using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Channels;

namespace IrisSVMSecured
{
	class SvmBatchPoly
	{
		private const bool   RunSvc      = true;
		private const bool   RunIrisSvc  = false;
		private const string OutputDir    = @"C:\Output\";

       
		public class SecureSvc
		{
			private enum Kernel
			{
				Linear,
				Poly,
				Rbf,
				Sigmoid
			}

			//private int _nClasses;
			//private int _nRows;

			//private int[] classes;
			private readonly double[][]		_vectors;
			private readonly double[][]		_coefficients;
			private readonly double[]		_intercepts;
			private readonly Kernel			_kernel;
			private readonly double			_gamma;
			private readonly double			_coef0;
			private readonly ulong			_degree;
			private readonly int			_power;
			private readonly SEALContext	_context;
			private readonly PublicKey		_publicKey;
			private readonly SecretKey		_secretKey;
			private readonly RelinKeys		_relinKeys;
			private readonly GaloisKeys		_galoisKeys;
			private readonly Encryptor		_encryptor;
			private readonly Evaluator		_evaluator;
			private readonly Decryptor		_decryptor;
			private readonly CKKSEncoder	_encoder;

			//private static bool _firstTime = true;

			private const bool PRINT_SCALE = false;
            private const bool PrintExactScale = false;
            private const bool PrintCipherText = false;
            //private static Decryptor _decryptor;

            public SecureSvc(int nRows, double[][] vectors, double[][] coefficients, double[] intercepts,
				 String kernel, double gamma, double coef0, ulong degree , int power)
			{


				//this._nRows			= nRows;

				this._vectors		= vectors;
				this._coefficients	= coefficients;
				this._intercepts		= intercepts;

				this._kernel			= Enum.Parse<Kernel>(kernel, true);
				this._gamma			= gamma;
				this._coef0			= coef0;
				this._degree			= degree;
				this._power			= power;
				EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);

				ulong polyModulusDegree = 16384;
				
				if (power >= 20 && power < 40 )
				{
					parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
						new int[] { 60, 20, 21, 22, 23, 24, 25, 26, 27, 60 });
				}
				else if (power >= 40 && power < 60)
				{
					parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
						new int[] { 60, 40, 40, 40, 40, 40, 40, 40 , 60 });
				}
				else if (power == 60)
				{
					polyModulusDegree = 32768;
                    parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
						new int[] { 60, 60, 60, 60, 60, 60, 60, 60, 60 });
				}
				parms.PolyModulusDegree = polyModulusDegree;
                _context = new SEALContext(parms);

				KeyGenerator keygen = new KeyGenerator(_context);
				_publicKey = keygen.PublicKey;
				_secretKey = keygen.SecretKey;
				_relinKeys = keygen.RelinKeys();
				_galoisKeys = keygen.GaloisKeys();

				_encryptor = new Encryptor(_context, _publicKey);
				_evaluator = new Evaluator(_context);
				_decryptor = new Decryptor(_context, _secretKey);
				_encoder = new CKKSEncoder(_context);
			}

			public int Predict(double[] features, bool useRelinearizeInplace,bool useReScale,out double finalResult)
			{

			   
				Console.WriteLine();

				ulong slotCount = _encoder.SlotCount;
				
				//Console.WriteLine($"Number of slots: {slotCount}");

				var featuresLength = features.Length;



				var plaintexts  = new Plaintext();
				var featuresCiphertexts = new Ciphertext();
				
				Stopwatch clientStopwatch = new Stopwatch();
				clientStopwatch.Start();
                //Encode and encrypt features
                double scale = Math.Pow(2.0, _power);
				_encoder.Encode(features, scale, plaintexts);
				_encryptor.Encrypt(plaintexts, featuresCiphertexts);
				PrintScale(plaintexts, "featurePlaintext");
				PrintScale(featuresCiphertexts, "featurefEncrypted");
				clientStopwatch.Stop();


				Stopwatch serverInitStopwatch = new Stopwatch();
				serverInitStopwatch.Start();
                // Handle SV
                var numOfrowsCount    = _vectors.Length;
				var numOfcolumnsCount = _vectors[0].Length;
		   
				var svPlaintexts = new Plaintext[numOfrowsCount];



                //Encode SV
                var sums = new Ciphertext[numOfrowsCount];
                for (int i = 0; i < numOfrowsCount; i++)
				{
						svPlaintexts[i] = new Plaintext();
						_encoder.Encode(_vectors[i], scale, svPlaintexts[i]);
						PrintScale(svPlaintexts[i], "supportVectorsPlaintext"+i);
						sums[i] = new Ciphertext();
                }

				var kernels      = new Ciphertext[numOfrowsCount];
				var decisionsArr = new Ciphertext[numOfrowsCount];
				var coefArr      = new Plaintext [numOfrowsCount];

				for (int i = 0; i < numOfrowsCount; i++)
				{
					kernels[i]       = new Ciphertext();
					decisionsArr[i]  = new Ciphertext();
					coefArr[i]       = new Plaintext();
				}
				Plaintext  gamaPlaintext= new Plaintext();
				_encoder.Encode(_gamma, scale, gamaPlaintext);

				Ciphertext tempCt = new Ciphertext();
				serverInitStopwatch.Stop();

				Stopwatch innerProductStopwatch = new Stopwatch();
                Stopwatch negateStopwatch = new Stopwatch();
                Stopwatch degreeStopwatch = new Stopwatch();
                // Level 1
                for (int i = 0; i < numOfrowsCount; i++)
				{
                    //Console.WriteLine(i);

                    //inner product
                  

                    innerProductStopwatch.Start();

                    _evaluator.MultiplyPlain(featuresCiphertexts, svPlaintexts[i],sums[i]);
                    int numOfRotations = (int)Math.Ceiling(Math.Log2(numOfcolumnsCount));

                    for (int k = 1,m=1; m <= numOfRotations/*(int)encoder.SlotCount/2*/; k <<= 1,m++)
                    {

                        _evaluator.RotateVector(sums[i], k, _galoisKeys, tempCt);
                        _evaluator.AddInplace(sums[i], tempCt);

                    }
                    innerProductStopwatch.Stop();
                    kernels[i] = sums[i];

                    PrintCyprherText(_decryptor, kernels[i], _encoder, $"inner product TotalValue {i}" );
                    PrintScale(kernels[i], "0. kernels" + i);
                    if (useRelinearizeInplace)
                    {
                        _evaluator.RelinearizeInplace(kernels[i], _relinKeys);
                    }

                    if (useReScale)
                    {
                        _evaluator.RescaleToNextInplace(kernels[i]);
                    }

                    PrintScale(kernels[i], "1. kernels" + i);
                    kernels[i].Scale = scale;


					if(_kernel == Kernel.Poly)
					{

						if (useReScale)
						{
							ParmsId lastParmsId = kernels[i].ParmsId;
							_evaluator.ModSwitchToInplace(gamaPlaintext, lastParmsId);
						}
						_evaluator.MultiplyPlainInplace(kernels[i], gamaPlaintext);
						PrintScale(kernels[i], "2. kernels" + i);
						if (useRelinearizeInplace)
						{
							_evaluator.RelinearizeInplace(kernels[i], _relinKeys);
						}

						if (useReScale)
						{
							_evaluator.RescaleToNextInplace(kernels[i]);
						}
						PrintScale(kernels[i], "3.  kernels" + i);

						if (Math.Abs(_coef0) > 0)
						{
							Plaintext coef0Plaintext = new Plaintext();
							_encoder.Encode(_coef0, kernels[i].Scale, coef0Plaintext);
							if (useReScale)
							{
								ParmsId lastParmsId = kernels[i].ParmsId;
								_evaluator.ModSwitchToInplace(coef0Plaintext, lastParmsId);
							}

							//kernels[i].Scale = coef0Plaintext.Scale;

							_evaluator.AddPlainInplace(kernels[i], coef0Plaintext);
                        }

                        PrintScale(kernels[i], "4.  kernels" + i);

                       

                        degreeStopwatch.Start();

                        var kernel = new Ciphertext(kernels[i]);
                        for (int d = 0; d < (int)_degree-1; d++)
						{

							kernel.Scale = kernels[i].Scale;
							if (useReScale)
							{
								ParmsId lastParmsId = kernels[i].ParmsId;
								_evaluator.ModSwitchToInplace(kernel, lastParmsId);
							}
                            _evaluator.MultiplyInplace(kernels[i], kernel);
							PrintScale(kernels[i], d + "  5. kernels" + i);
							if (useRelinearizeInplace)
							{
								_evaluator.RelinearizeInplace(kernels[i], _relinKeys);
							}

							if (useReScale)
							{
								_evaluator.RescaleToNextInplace(kernels[i]);
							}
							PrintScale(kernels[i], d + " rescale  6. kernels" + i);
						}
						PrintScale(kernels[i], "7. kernels" + i);

						degreeStopwatch.Stop();
                    }


					

					negateStopwatch.Start();

                    _evaluator.NegateInplace(kernels[i]);
                    negateStopwatch.Stop();

                    PrintScale(kernels[i], "8. kernel"+i); 

					PrintCyprherText(_decryptor, kernels[i], _encoder, "kernel"+i);

				}


                Stopwatch serverDecisionStopWatch = new Stopwatch();

                serverDecisionStopWatch.Start();

                // Encode coefficients : ParmsId! , scale!
                double scale2 = Math.Pow(2.0, _power);
				if (useReScale)
				{
					scale2 = kernels[0].Scale;
				}

				for (int i = 0; i < numOfrowsCount; i++)
				{
					_encoder.Encode(_coefficients[0][i], scale2, coefArr[i]);
					PrintScale(coefArr[i], "coefPlainText"+i);
				}



				if (useReScale)
				{
					for (int i = 0; i < numOfrowsCount; i++)
					{
						ParmsId lastParmsId = kernels[i].ParmsId;
						_evaluator.ModSwitchToInplace(coefArr[i], lastParmsId);
					}
				}
				// Level 2
				// Calculate decisionArr
                for (int i = 0; i < numOfrowsCount; i++)
				{
					_evaluator.MultiplyPlain(kernels[i], coefArr[i], decisionsArr[i]);
					if (useRelinearizeInplace)
					{
						_evaluator.RelinearizeInplace(decisionsArr[i], _relinKeys);
					}

					if (useReScale)
					{
						_evaluator.RescaleToNextInplace(decisionsArr[i]);
					}
					PrintScale(decisionsArr[i], "decision"+i);
					PrintCyprherText(_decryptor, decisionsArr[i], _encoder, "decision" + i);
				}



				// Calculate decisionTotal
				Ciphertext decisionTotal = new Ciphertext();
				//=================================================================
				_evaluator.AddMany(decisionsArr, decisionTotal);
				//=================================================================
			  
				PrintScale(decisionTotal, "decisionTotal"); 
				PrintCyprherText(_decryptor, decisionTotal, _encoder, "decision total");


				// Encode intercepts : ParmsId! , scale!
				Plaintext interceptsPlainText = new Plaintext();
				
				double scale3 = Math.Pow(2.0, _power*3);
				if (useReScale)
				{
					scale3 = decisionTotal.Scale;
				}
				_encoder.Encode(_intercepts[0], scale3, interceptsPlainText);
				if (useReScale)
				{
					ParmsId lastParmsId = decisionTotal.ParmsId;
					_evaluator.ModSwitchToInplace(interceptsPlainText, lastParmsId);
				}

				PrintScale(interceptsPlainText, "interceptsPlainText");
				PrintScale(decisionTotal, "decisionTotal");


				//// Calculate finalTotal
				Ciphertext finalTotal = new Ciphertext();

				//=================================================================
				_evaluator.AddPlainInplace(decisionTotal, interceptsPlainText);
				//=================================================================

				PrintScale(decisionTotal, "decisionTotal");  //Level 3
				List<double> result = PrintCyprherText(_decryptor, decisionTotal, _encoder, "finalTotal",true);

                serverDecisionStopWatch.Stop();

                Console.WriteLine($"client Init elapsed {clientStopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"server Init elapsed {serverInitStopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"server innerProductStopwatch elapsed {innerProductStopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"server negateStopwatch elapsed {negateStopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"server degreeStopwatch elapsed {degreeStopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"server Decision elapsed {serverDecisionStopWatch.ElapsedMilliseconds} ms");


                finalResult = result[0];

                if (result[0] > 0)
				{
					return 0;
				}

				return 1;

			}

			private static void PrintScale(Ciphertext ciphertext, String name)
			{
				if (!PRINT_SCALE) return;
				if (PrintExactScale) { 
					Console.Write($"    + Exact scale of {name}:");
				Console.WriteLine(" {0:0.0000000000}", ciphertext.Scale);
				}

			Console.WriteLine("    + Scale of {0}: {1} bits ", name,
					Math.Log(ciphertext.Scale, newBase: 2)/*, _decryptor.InvariantNoiseBudget(ciphertext)*/);
			}

			private static void PrintScale(Plaintext plaintext, String name)
			{
				if (!PRINT_SCALE) return;
                if (PrintExactScale)
				{
					Console.Write($"    + Exact scale of {name}:");
					Console.WriteLine(" {0:0.0000000000}", plaintext.Scale);
                }
				
				Console.WriteLine("    + Scale of {0}: {1} bits", name,
					Math.Log(plaintext.Scale, newBase: 2));
			}

			private static List<double> PrintCyprherText(Decryptor decryptor, Ciphertext ciphertext, CKKSEncoder encoder,String name,bool print=false)
			{
				if (!PrintCipherText&& !print) return null;
				Plaintext plainResult = new Plaintext();
				decryptor.Decrypt(ciphertext, plainResult);
				List<double> result = new List<double>();
				encoder.Decode(plainResult, result);

				Console.WriteLine($"{name} TotalValue = {result[0]}");
				return result;
			}


		}


		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");


			//var max = CoeffModulus.MaxBitCount(16384);

		//	Console.WriteLine(max);

            double[][] features;
			int numOfRows = 0;
			if (args.Length >= 4)
			{
				numOfRows = args.Length / 4;
				// Features:
				features = new double[numOfRows][];
				for (int i = 0; i < numOfRows; i++)
				{
					features[i] = new double[4];
				}
				for (int i = 0, l = args.Length; i < l; i++)
				{
					features[i/4][i%4] = Double.Parse(args[i]);
				}
			}
			else
			{
				List<double[]> rows = new List<double[]>();
				var bytes = Properties.Resources.iris;
				numOfRows = 0;
				Stream stream = new MemoryStream(bytes);
				using (TextFieldParser csvParser = new TextFieldParser(stream))
				{
					csvParser.CommentTokens = new string[] { "#" };
					csvParser.SetDelimiters(new string[] { "," });
					csvParser.HasFieldsEnclosedInQuotes = true;

					while (!csvParser.EndOfData)
					{
						// Read current line fields, pointer moves to the next line.
						string[] readFields = csvParser.ReadFields();
						double[] doubleValues = new double[readFields.Length];

						for (int j = 0; j < 4; j++)
						{
							doubleValues[j] = Double.Parse(readFields[j]);
						}
						rows.Add(doubleValues);
						numOfRows++;
					}
				}

				features = new double[numOfRows][];
				for (int i = 0; i < numOfRows; i++)
				{

					features[i] = rows[i]; 
				}
			}


			double[][] vectors = new double[3][];

			vectors[0] = new[] { 5.1, 3.3, 1.7, 0.5 };
			vectors[1] = new[] { 4.5, 2.3, 1.3, 0.3 };
			vectors[2] = new[] { 5.1, 2.5, 3.0, 1.1 };

			double[][] coefficients = new double[1][];
			coefficients[0] = new double[] { -0.008885899026071108, -0.0005100630977269122, 0.009395962123798021 };
			double[] intercepts = { 1.1358388232934824 };


			Console.WriteLine("\n\n SecureSVC : ");
			int scale = 40;
			SecureSvc clf3 =
				new SecureSvc(2, vectors, coefficients, intercepts, "poly", 0.25, 0.0, 3,scale);
			;
			
			bool useRelinearizeInplace = true;
			bool useReScale = true;

			
			using (System.IO.StreamWriter file =
				new System.IO.StreamWriter(
					$@"{OutputDir}SVMBatchPoly_IrisSecureSVC_classification_result_{scale}_{useRelinearizeInplace}_{useReScale}.txt")
			)
			{
				Stopwatch timePredictSum = new Stopwatch();
				for (int i = 0; i < numOfRows; i++)
				{
					Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
					double finalResult = 0;
					timePredictSum.Start();
					int estimation = clf3.Predict(features[i],useRelinearizeInplace,useReScale,out finalResult);
					timePredictSum.Stop();
					file.WriteLine($"SecureSVC estimation{i} is : {estimation}  finalResult = {finalResult}");
					Console.WriteLine($"SecureSVC estimation{i} is : {estimation}  finalResult = {finalResult}");
					Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
				}
				
				int avgPredict = (int)(timePredictSum.Elapsed.TotalMilliseconds  / numOfRows);
				Console.WriteLine($"Average Predict: {avgPredict} ms");
				file.WriteLine($"Average Predict: {avgPredict} ms");
				file.WriteLine($"Total time Predict: {timePredictSum.Elapsed.TotalMilliseconds} ms");
            }


			Console.WriteLine("End , press Enter to quit");


			Console.ReadLine();

		}
	}
}

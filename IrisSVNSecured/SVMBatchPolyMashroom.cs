using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Channels;

namespace IrisSVNSecured
{


	internal static class ExtensionMethods
	{
		internal static T[][] ToJaggedArray<T>(this T[,] twoDimensionalArray)
		{
			int rowsFirstIndex = twoDimensionalArray.GetLowerBound(0);
			int rowsLastIndex = twoDimensionalArray.GetUpperBound(0);
			int numberOfRows = rowsLastIndex + 1;

			int columnsFirstIndex = twoDimensionalArray.GetLowerBound(1);
			int columnsLastIndex = twoDimensionalArray.GetUpperBound(1);
			int numberOfColumns = columnsLastIndex + 1;

			T[][] jaggedArray = new T[numberOfRows][];
			for (int i = rowsFirstIndex; i <= rowsLastIndex; i++)
			{
				jaggedArray[i] = new T[numberOfColumns];

				for (int j = columnsFirstIndex; j <= columnsLastIndex; j++)
				{
					jaggedArray[i][j] = twoDimensionalArray[i, j];
				}
			}
			return jaggedArray;
		}
	}
    class SVMBatchPolyMashroom
    {
		private const bool   RunSvc      = false;
		private const bool   RunIris  = false;
		private const bool   RunMashroom = true;

        private const string OutputDir    = @"C:\Output\";

        public class SVC
		{
			private enum Kernel
			{
				Linear,
				Poly,
				Rbf,
				Sigmoid
			}

			private readonly int			_nClasses;
			private readonly int			_nRows;
			private readonly int[]			_classes;
			private readonly double[][]		_vectors;
			private readonly double[][]		_coefficients;
			private readonly double[]		_intercepts;
			private readonly int[]			_weights;
			private readonly Kernel			_kernel;
			private readonly double			_gamma;
			private readonly double			_coef0;
			private readonly double			_degree;

			public SVC(int nClasses, int nRows, double[][] vectors, double[][] coefficients, double[] intercepts,
				int[] weights, String kernel, double gamma, double coef0, double degree)
			{


				Console.WriteLine($"Create SVC with nClasses = {nClasses} , ");
				this._nClasses = nClasses;
				this._classes = new int[nClasses];
				for (int i = 0; i < nClasses; i++)
				{
					this._classes[i] = i;
				}

				this._nRows = nRows;

				this._vectors = vectors;
				this._coefficients = coefficients;
				this._intercepts = intercepts;
				this._weights = weights;

				this._kernel = Enum.Parse<Kernel>(kernel, true);
				this._gamma = gamma;
				this._coef0 = coef0;
				this._degree = degree;
			}

			public int Predict(double[] features, out double finalResult)
			{

				double[] kernels = new double[_vectors.Length];
				double kernel;
				switch (this._kernel)
				{
					case Kernel.Linear:
						// <x,x'>
						for (int i = 0; i < this._vectors.Length; i++)
						{
							kernel = 0;
							for (int j = 0; j < this._vectors[i].Length; j++)
							{
								kernel += this._vectors[i][j] * features[j];
								//Console.WriteLine($"kernel += this.vectors[{i}][{j}] * features[{j}]");
							}

							kernels[i] = kernel;
							//Console.WriteLine($"kernels[{i}] = {kernel}");
							//Console.WriteLine("-----------------------------------------------------");
						}

						break;
					case Kernel.Poly:
						// (y<x,x'>+r)^d
						for (int i = 0; i < this._vectors.Length; i++)
						{
							kernel = 0;
							for (int j = 0; j < this._vectors[i].Length; j++)
							{
								kernel += this._vectors[i][j] * features[j];
							}
							Console.WriteLine($"inner product result {i} : {kernel}");
                            kernels[i] = Math.Pow((this._gamma * kernel) + this._coef0, this._degree);
							Console.WriteLine($"kernels[{i}] = {kernels[i]}");
                        }

						break;
					case Kernel.Rbf:
						// exp(-y|x-x'|^2)
						for (int i = 0; i < this._vectors.Length; i++)
						{
							kernel = 0;
							for (int j = 0; j < this._vectors[i].Length; j++)
							{
								kernel += Math.Pow(this._vectors[i][j] - features[j], 2);
							}

							kernels[i] = Math.Exp(-this._gamma * kernel);
						}

						break;
					case Kernel.Sigmoid:
						// tanh(y<x,x'>+r)
						for (int i = 0; i < this._vectors.Length; i++)
						{
							kernel = 0;
							for (int j = 0; j < this._vectors[i].Length; j++)
							{
								kernel += this._vectors[i][j] * features[j];
							}

							kernels[i] = Math.Tanh((this._gamma * kernel) + this._coef0);
						}

						break;
				}

				Console.WriteLine("Calculate weights : ");
				int[] starts = new int[this._nRows];
				for (int i = 0; i < this._nRows; i++)
				{
					if (i != 0)
					{
						int start = 0;
						for (int j = 0; j < i; j++)
						{
							start += this._weights[j];
						}

						starts[i] = start;
						//Console.WriteLine($"starts[{i}] = {start}");

					}
					else
					{
						starts[0] = 0;
						//Console.WriteLine($"starts[0] = 0");
					}
				}

				int[] ends = new int[this._nRows];
				for (int i = 0; i < this._nRows; i++)
				{
					ends[i] = this._weights[i] + starts[i];
					//Console.WriteLine($"ends[{i}] = this.weights[{i}] + starts[{i}]");
				}

				if (this._nClasses == 2)
				{

					for (int i = 0; i < kernels.Length; i++)
					{
						kernels[i] = -kernels[i];
					}

					double decision = 0;
					for (int k = starts[1]; k < ends[1]; k++)
					{
						decision += kernels[k] * this._coefficients[0][k];
						//Console.WriteLine($"starts1 : decision += kernels[{k}] * this.coefficients[0][{k}]");
						//Console.WriteLine($"starts1 : decision = {decision}");
					}

					for (int k = starts[0]; k < ends[0]; k++)
					{
						decision += kernels[k] * this._coefficients[0][k];
						//Console.WriteLine($"starts0 : decision += kernels[{k}] * this.coefficients[0][{k}]");
						//Console.WriteLine($"starts0 : decision = {decision}");
					}

					//Console.WriteLine($"Total decision = {decision}");
					decision += this._intercepts[0];
					
					Console.WriteLine($"decision = {decision}");
					finalResult = decision;

                    if (decision > 0)
					{
						return 0;
					}

					return 1;

				}

				double[] decisions = new double[this._intercepts.Length];
				for (int i = 0, d = 0, l = this._nRows; i < l; i++)
				{
					for (int j = i + 1; j < l; j++)
					{
						double tmp = 0;
						for (int k = starts[j]; k < ends[j]; k++)
						{
							tmp += this._coefficients[i][k] * kernels[k];
						}

						for (int k = starts[i]; k < ends[i]; k++)
						{
							tmp += this._coefficients[j - 1][k] * kernels[k];
						}

						decisions[d] = tmp + this._intercepts[d];
						d++;
					}
				}

				int[] votes = new int[this._intercepts.Length];
				for (int i = 0, d = 0, l = this._nRows; i < l; i++)
				{
					for (int j = i + 1; j < l; j++)
					{
						votes[d] = decisions[d] > 0 ? i : j;
						d++;
					}
				}

				int[] amounts = new int[this._nClasses];
				for (int i = 0, l = votes.Length; i < l; i++)
				{
					amounts[votes[i]] += 1;
				}

				int classVal = -1, classIdx = -1;
				for (int i = 0, l = amounts.Length; i < l; i++)
				{
					if (amounts[i] > classVal)
					{
						classVal = amounts[i];
						classIdx = i;
					}
				}

				finalResult = -1;

                return this._classes[classIdx];

			}
		}


		public class SecureSVC
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
			private int[]			        _weights;
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
			private readonly int			_numOfrowsCount;
			private readonly int			 _numOfcolumnsCount;
			private readonly Plaintext[]	_svPlaintexts;
			private readonly Ciphertext[]	_sums;
			private readonly Ciphertext[]	_kernels;
			private readonly Ciphertext[]	_decisionsArr;
			private readonly Plaintext[]	_coefArr;
			private readonly Plaintext	_gamaPlaintext;
			private Plaintext[,] _svPlaintextsArr;
			private Ciphertext[] _innerProdSums;
			private double _scale;
			private const double ZERO = 1.0E-9;

            //private static bool _firstTime = true;

            private const bool PRINT_SCALE		 = false;
            private const bool PRINT_EXACT_SCALE = false;
            private const bool PRINT_CIPHER_TEXT = false;

            private const bool USE_BATCH_INNER_PRODUCT = false;


            //private static Decryptor _decryptor;

            public SecureSVC(int nRows, double[][] vectors, double[][] coefficients, double[] intercepts,
				int[] weights, String kernel, double gamma, double coef0, ulong degree , int power)
			{


                //this._nRows			= nRows;

                this._vectors		= vectors;
				this._coefficients	= coefficients;
				this._intercepts		= intercepts;
				this._weights		= weights;

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
				if (USE_BATCH_INNER_PRODUCT)
				{
					_galoisKeys = keygen.GaloisKeys();
				}

				_encryptor = new Encryptor(_context, _publicKey);
				_evaluator = new Evaluator(_context);
				_decryptor = new Decryptor(_context, _secretKey);
				_encoder = new CKKSEncoder(_context);



				Stopwatch serverInitStopwatch = new Stopwatch();
				serverInitStopwatch.Start();



                _numOfrowsCount = _vectors.Length;
				_numOfcolumnsCount = _vectors[0].Length;
				_scale = Math.Pow(2.0, _power);
                ////////////////////////////////////////////////////////////////////////
                ///
                ///	vars for batch rotations
                /// 
                _svPlaintexts = new Plaintext[_numOfrowsCount];
			
                //Encode SV
                _sums = new Ciphertext[_numOfrowsCount];
                if (USE_BATCH_INNER_PRODUCT)
                {
	                for (int i = 0; i < _numOfrowsCount; i++)
	                {
		                _svPlaintexts[i] = new Plaintext();
		                _encoder.Encode(_vectors[i], _scale, _svPlaintexts[i]);
		                PrintScale(_svPlaintexts[i], "batch supportVectorsPlaintext" + i);
		                _sums[i] = new Ciphertext();
	                }
                }

                /////////////////////////////////////////////////////////////////////////
				//
                //   vars for simple inner product
				//
                // Handle SV

                _svPlaintextsArr = new Plaintext[_numOfrowsCount, _numOfcolumnsCount];

                //Encode SV
                for (int i = 0; i < _numOfrowsCount; i++)
                {
	                for (int j = 0; j < _numOfcolumnsCount; j++)
	                {
		                _svPlaintextsArr[i, j] = new Plaintext();
		                
		                _encoder.Encode(_vectors[i][j] !=0 ? _vectors[i][j] : ZERO, _scale, _svPlaintextsArr[i, j]);
		                PrintScale(_svPlaintextsArr[i, j], $"supportVectorsPlaintext[{i}][{j}]");
	                }
                }

                // Prepare sum of inner product
                _innerProdSums = new Ciphertext[_numOfcolumnsCount];
                for (int i = 0; i < _numOfcolumnsCount; i++)
                {
	                _innerProdSums[i] = new Ciphertext();
                }
				//////////////////////////////////////////////////////////////




                _kernels = new Ciphertext[_numOfrowsCount];
				_decisionsArr = new Ciphertext[_numOfrowsCount];
				_coefArr = new Plaintext[_numOfrowsCount];

				for (int i = 0; i < _numOfrowsCount; i++)
				{
					_kernels[i] = new Ciphertext();
					_decisionsArr[i] = new Ciphertext();
					_coefArr[i] = new Plaintext();
				}
				_gamaPlaintext = new Plaintext();
				_encoder.Encode(_gamma!=0?_gamma: ZERO , _scale, _gamaPlaintext);

				
				serverInitStopwatch.Stop();
				Console.WriteLine($"server Init elapsed {serverInitStopwatch.ElapsedMilliseconds} ms");
            }

			public int Predict(double[] features, bool useRelinearizeInplace,bool useReScale,out double finalResult)
			{

				
                Console.WriteLine();

				ulong slotCount = _encoder.SlotCount;
				
				//Console.WriteLine($"Number of slots: {slotCount}");

				var featuresLength = features.Length;

				//double scale = Math.Pow(2.0, _power);





                var plaintexts  = new Plaintext();
				var featuresCiphertexts = new Ciphertext();

				Stopwatch clientStopwatch = new Stopwatch();
				clientStopwatch.Start();
				//Encode and encrypt features
				///////////////////////////////////////////////////////////////////////////////////////////////////
				_encoder.Encode(features, _scale, plaintexts);
				_encryptor.Encrypt(plaintexts, featuresCiphertexts);
				PrintScale(plaintexts, "featurePlaintext");
				PrintScale(featuresCiphertexts, "featurefEncrypted");
                ///////////////////////////////////////////////////////////////////////////////////////////////////
                var plaintexts_arr = new Plaintext[featuresLength];
                var featuresCiphertexts_arr = new Ciphertext[featuresLength];
                //Encode and encrypt features
                for (int i = 0; i < featuresLength; i++)
                {
	                plaintexts_arr[i] = new Plaintext();

	                _encoder.Encode(features[i], _scale, plaintexts_arr[i]);

	                PrintScale(plaintexts_arr[i], "featurePlaintext" + i);
	                featuresCiphertexts_arr[i] = new Ciphertext();

	                _encryptor.Encrypt(plaintexts_arr[i], featuresCiphertexts_arr[i]);
	                PrintScale(featuresCiphertexts_arr[i], "featurefEncrypted" + i);
                }
                //////////////////////////////////////////////////////////////////////////////////////////////////
                clientStopwatch.Stop();

				// Handle SV


				Stopwatch innerProductStopwatch = new Stopwatch();
                Stopwatch negateStopwatch = new Stopwatch();
                Stopwatch degreeStopwatch = new Stopwatch();

                Ciphertext tempCt = new Ciphertext();




                // Level 1
                for (int i = 0; i < _numOfrowsCount; i++)
				{
					//Console.WriteLine(i);

                    //inner product
                    innerProductStopwatch.Start();
                    if (USE_BATCH_INNER_PRODUCT)
                    {
	                    _kernels[i] = InnerProduct(featuresCiphertexts, _svPlaintexts, i, _sums, _numOfcolumnsCount,
		                    tempCt);
                    }
                    else
                    {
	                    //inner product
	                    for (int j = 0; j < _numOfcolumnsCount; j++)
	                    {
		                    _evaluator.MultiplyPlain(featuresCiphertexts_arr[j], _svPlaintextsArr[i, j], _innerProdSums[j]);

		                    if (useRelinearizeInplace)
		                    {
			                    _evaluator.RelinearizeInplace(_innerProdSums[j], _relinKeys);
		                    }

		                    if (useReScale)
		                    {
			                    _evaluator.RescaleToNextInplace(_innerProdSums[j]);
		                    }

		                    PrintScale(_innerProdSums[j], "tSum" + j);

	                    }
	                    _evaluator.AddMany(_innerProdSums, _kernels[i]);
                    }


                    //kernels[i] = sums[i];
                    innerProductStopwatch.Stop();
                    PrintCyprherText(_decryptor, _kernels[i], _encoder, $"inner product result {i}" );
                    PrintScale(_kernels[i], "0. kernels" + i);
                    if (useRelinearizeInplace)
                    {
                        _evaluator.RelinearizeInplace(_kernels[i], _relinKeys);
                    }

                    if (useReScale)
                    {
                        _evaluator.RescaleToNextInplace(_kernels[i]);
                    }

                    PrintScale(_kernels[i], "1. kernels" + i);
                    _kernels[i].Scale = _scale;


                    if (_kernel == Kernel.Poly)
					{

                        if (useReScale)
                        {
                            ParmsId lastParmsId = _kernels[i].ParmsId;
                            _evaluator.ModSwitchToInplace(_gamaPlaintext, lastParmsId);
                        }
                        _evaluator.MultiplyPlainInplace(_kernels[i], _gamaPlaintext);
                        PrintScale(_kernels[i], "2. kernels" + i);
                        if (useRelinearizeInplace)
                        {
                            _evaluator.RelinearizeInplace(_kernels[i], _relinKeys);
                        }

                        if (useReScale)
                        {
                            _evaluator.RescaleToNextInplace(_kernels[i]);
                        }
                        PrintScale(_kernels[i], "3.  kernels" + i);

                        if (Math.Abs(_coef0) > 0)
                        {
                            Plaintext coef0Plaintext = new Plaintext();
                            _encoder.Encode(_coef0, _kernels[i].Scale, coef0Plaintext);
                            if (useReScale)
                            {
                                ParmsId lastParmsId = _kernels[i].ParmsId;
                                _evaluator.ModSwitchToInplace(coef0Plaintext, lastParmsId);
                            }

                            //kernels[i].Scale = coef0Plaintext.Scale;

                            _evaluator.AddPlainInplace(_kernels[i], coef0Plaintext);
                        }

                        PrintScale(_kernels[i], "4.  kernels" + i);
                        degreeStopwatch.Start();
                        var kernel = new Ciphertext(_kernels[i]);
                        for (int d = 0; d < (int)_degree - 1; d++)
                        {

                            kernel.Scale = _kernels[i].Scale;
                            if (useReScale)
                            {
                                ParmsId lastParmsId = _kernels[i].ParmsId;
                                _evaluator.ModSwitchToInplace(kernel, lastParmsId);
                            }
                            _evaluator.MultiplyInplace(_kernels[i], kernel);
                            PrintScale(_kernels[i], d + "  5. kernels" + i);
                            if (useRelinearizeInplace)
                            {
                                _evaluator.RelinearizeInplace(_kernels[i], _relinKeys);
                            }

                            if (useReScale)
                            {
                                _evaluator.RescaleToNextInplace(_kernels[i]);
                            }
                            PrintScale(_kernels[i], d + " rescale  6. kernels" + i);
                        }
                        PrintScale(_kernels[i], "7. kernels" + i);
						degreeStopwatch.Stop();
					}



					negateStopwatch.Start();

					_evaluator.NegateInplace(_kernels[i]);
                    negateStopwatch.Stop();

					PrintScale(_kernels[i], "8. kernel"+i); 

					PrintCyprherText(_decryptor, _kernels[i], _encoder, "kernel"+i);

				}
                Stopwatch serverDecisionStopWatch = new Stopwatch();
                serverDecisionStopWatch.Start();
                // Encode coefficients : ParmsId! , scale!
                double scale2 = Math.Pow(2.0, _power);
				if (useReScale)
				{
					scale2 = _kernels[0].Scale;
				}

				for (int i = 0; i < _numOfrowsCount; i++)
				{
					_encoder.Encode(_coefficients[0][i], scale2, _coefArr[i]);
					PrintScale(_coefArr[i], "coefPlainText"+i);
				}



				if (useReScale)
				{
					for (int i = 0; i < _numOfrowsCount; i++)
					{
						ParmsId lastParmsId = _kernels[i].ParmsId;
						_evaluator.ModSwitchToInplace(_coefArr[i], lastParmsId);
					}
				}
                // Level 2
                // Calculate decisionArr
                for (int i = 0; i < _numOfrowsCount; i++)
                {
                    _evaluator.MultiplyPlain(_kernels[i], _coefArr[i], _decisionsArr[i]);
                    if (useRelinearizeInplace)
                    {
                        _evaluator.RelinearizeInplace(_decisionsArr[i], _relinKeys);
                    }

                    if (useReScale)
                    {
                        _evaluator.RescaleToNextInplace(_decisionsArr[i]);
                    }
                    PrintScale(_decisionsArr[i], "decision" + i);
                    PrintCyprherText(_decryptor, _decisionsArr[i], _encoder, "decision" + i);
                }



                // Calculate decisionTotal
                Ciphertext decisionTotal = new Ciphertext();
				//=================================================================
				_evaluator.AddMany(_decisionsArr, decisionTotal);
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
				
				//using (System.IO.StreamWriter file =
				//	new System.IO.StreamWriter(
				//		$@"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\GeneralPolyBatch_IrisSecureSVC_total_{power}_{useRelinearizeInplace}_{useReScale}.txt", !_firstTime)
				//)
				//{
				//	_firstTime = false;
				//	file.WriteLine($"{result[0]}");
                serverDecisionStopWatch.Stop();
				//}
                Console.WriteLine($"client Init elapsed {clientStopwatch.ElapsedMilliseconds} ms");
                
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

			private Ciphertext InnerProduct(Ciphertext featuresCiphertexts, Plaintext[] svPlaintexts, int i, Ciphertext[] sums,int numOfcolumnsCount, Ciphertext tempCt)
			{
				_evaluator.MultiplyPlain(featuresCiphertexts, svPlaintexts[i], sums[i]);
				int numOfRotations = (int) Math.Ceiling(Math.Log2(numOfcolumnsCount));

				for (int k = 1, m = 1; m <= numOfRotations /*(int)encoder.SlotCount/2*/; k <<= 1, m++)
				{
					_evaluator.RotateVector(sums[i], k, _galoisKeys, tempCt);
					_evaluator.AddInplace(sums[i], tempCt);
				}

				var sum = sums[i];
				return sum;
			}

			private static void PrintScale(Ciphertext ciphertext, String name)
			{
				if (!PRINT_SCALE) return;
				if (PRINT_EXACT_SCALE) { 
					Console.Write($"    + Exact scale of {name}:");
				Console.WriteLine(" {0:0.0000000000}", ciphertext.Scale);
				}

			Console.WriteLine("    + Scale of {0}: {1} bits ", name,
					Math.Log(ciphertext.Scale, newBase: 2)/*, _decryptor.InvariantNoiseBudget(ciphertext)*/);
			}

			private static void PrintScale(Plaintext plaintext, String name)
			{
				if (!PRINT_SCALE) return;
                if (PRINT_EXACT_SCALE)
				{
					Console.Write($"    + Exact scale of {name}:");
					Console.WriteLine(" {0:0.0000000000}", plaintext.Scale);
                }
				
				Console.WriteLine("    + Scale of {0}: {1} bits", name,
					Math.Log(plaintext.Scale, newBase: 2));
			}

			private static List<double> PrintCyprherText(Decryptor decryptor, Ciphertext ciphertext, CKKSEncoder encoder,String name,bool print=false)
			{
				if (!PRINT_CIPHER_TEXT&& !print) return null;
				Plaintext plainResult = new Plaintext();
				decryptor.Decrypt(ciphertext, plainResult);
				List<double> result = new List<double>();
				encoder.Decode(plainResult, result);

				Console.WriteLine($"{name} result = {result[0]}");
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
			bool useRelinearizeInplace = true;
			bool useReScale = true;
			int scale = 40;

            if (RunIris)
			{
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
                        features[i / 4][i % 4] = Double.Parse(args[i]);
                    }
                }
                else
                {

                    var bytes = Properties.Resources.iris;
                    numOfRows = 0;
                    features = LoadFeatures(bytes,4, ref numOfRows);
                }


                double[][] vectors = new double[3][];

                vectors[0] = new[] { 5.1, 3.3, 1.7, 0.5 };
                vectors[1] = new[] { 4.5, 2.3, 1.3, 0.3 };
                vectors[2] = new[] { 5.1, 2.5, 3.0, 1.1 };

                double[][] coefficients = new double[1][];
                coefficients[0] = new double[] { -0.008885899026071108, -0.0005100630977269122, 0.009395962123798021 };
                double[] intercepts = { 1.1358388232934824 };
                int[] weights = { 2, 1 };
                 
                if (RunSvc)
                {
                    Console.WriteLine("SVC : ");
                    Stopwatch constructorPredictSum = new Stopwatch();
                    constructorPredictSum.Start();
                    SVC clf = new SVC(2, 2, vectors, coefficients, intercepts, weights, "poly", 0.25, 0.0, 3);
                    constructorPredictSum.Stop();
                    using (System.IO.StreamWriter file =
                        new System.IO.StreamWriter(
                            $@"{OutputDir}SVMBatchPoly_RunSvc_classification_result.txt")
                    )
                    {
                        Stopwatch timePredictSum = new Stopwatch();
                        for (int i = 0; i < numOfRows; i++)
                        {
                            timePredictSum.Start();
                            int estimation = clf.Predict(features[i], out var finalResult);
                            timePredictSum.Stop();
                            Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
                            Console.WriteLine($"SVC estimation{i} is : {estimation} ");
                            file.WriteLine($"SVC estimation{i} is : {estimation} finalResult = {finalResult}");
                            Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
                        }
                        int avgPredict = (int)(timePredictSum.Elapsed.TotalMilliseconds / numOfRows);
                        Console.WriteLine($"Constructor: {constructorPredictSum.Elapsed.TotalMilliseconds} ms");
                        Console.WriteLine($"Average Predict: {avgPredict} ms");
                        Console.WriteLine($"Total time Predict: {timePredictSum.Elapsed.TotalMilliseconds} ms");
                        file.WriteLine($"Constructor: {constructorPredictSum.Elapsed.TotalMilliseconds} ms");
                        file.WriteLine($"Average Predict: {avgPredict} ms");
                        file.WriteLine($"Total time Predict: {timePredictSum.Elapsed.TotalMilliseconds} ms");
                    }
                }


                Console.WriteLine("\n\n SecureSVC : ");
                //int scale = 40;
                SecureSVC clf3 =
                    new SecureSVC(2, vectors, coefficients, intercepts, weights, "poly", 0.25, 0.0, 3, scale);
                ;
                //IrisSVC clf = new IrisSVC( 2, vectors, coefficients, intercepts/*, weights, "poly"*/, 0.25, 0.0, 3);




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
                        int estimation = clf3.Predict(features[i], useRelinearizeInplace, useReScale, out finalResult);
                        timePredictSum.Stop();
                        file.WriteLine($"SecureSVC estimation{i} is : {estimation}  finalResult = {finalResult}");
                        Console.WriteLine($"SecureSVC estimation{i} is : {estimation}  finalResult = {finalResult}");
                        Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
                    }

                    int avgPredict = (int)(timePredictSum.Elapsed.TotalMilliseconds / numOfRows);
                    Console.WriteLine($"Average Predict: {avgPredict} ms");
                    file.WriteLine($"Average Predict: {avgPredict} ms");
                    file.WriteLine($"Total time Predict: {timePredictSum.Elapsed.TotalMilliseconds} ms");
                }

            }

            if (!RunMashroom)
	            return;


            double[,] vectors_mashroom_d		= { { -0.8403433999584713, 0.9532703900465632, -0.9838993878642219, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.14012794477794924, -1.376724168039617, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 1.4284264144452141, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.21699152073434588, -1.4861569457592787, -1.376724168039617, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, -2.3724904782364917, 0.622441390325499, -0.94101658068771, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, 0.28570977801184116 }, { -0.21699152073434588, -1.4861569457592787, -1.376724168039617, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, -0.94101658068771, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, 0.28570977801184116 }, { -2.087047158406722, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.970316152682646, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.2289977647710673, -1.1448057498013176, 0.8389893256787506, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, -0.5910746076888268, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, -0.250470603921817, -0.5143892000079515, -0.2957296600062443 }, { -0.8403433999584713, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.21699152073434588, 0.14012794477794924, 1.3730492931881484, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -1.0459524004866725, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 1.4284264144452141, -1.313108210112041, -0.2957296600062443 }, { 1.029712237713905, -1.4861569457592787, -0.19824982751343181, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, 0.6184260923501674, -1.1448057498013176, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 1.0830488202002277, 2.0300280920660976 }, { -0.8403433999584713, 0.14012794477794924, 1.7658740733635434, 1.185916567160356, -1.970316152682646, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.9532703900465632, 0.1945749526619632, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 1.4284264144452141, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, 0.14012794477794924, -0.19824982751343181, -0.8432296400028142, 0.40656202865404567, -6.1388691373667745, -0.4388636369510842, -0.6690383093994308, 1.7483245685118138, -1.1448057498013176, -1.0459524004866725, 0.6837776537937139, 0.5863846591895536, -0.42928778084845187, -0.4166805929300313, 0.0, -3.9790548744261973, -0.2561317410190009, 0.9480808566164142, -1.5096433676970904, 0.2843298100961381, 0.28570977801184116 }, { -0.8403433999584713, 0.9532703900465632, 0.1945749526619632, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 1.4284264144452141, 1.0830488202002277, 1.448588654048012 }, { 1.029712237713905, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, -1.4810169520224028, -1.4653525684453887, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, -0.5910746076888268, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, -0.250470603921817, -2.9105462303202203, -0.2957296600062443 }, { 1.029712237713905, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.9532703900465632, -1.376724168039617, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, -2.534652044595703, 0.5863846591895536, 0.622441390325499, -0.94101658068771, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, 0.28570977801184116 }, { -2.087047158406722, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.970316152682646, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, -1.1448057498013176, 0.8389893256787506, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, -1.4861569457592787, -0.19824982751343181, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, 0.6184260923501674, -1.1448057498013176, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, 2.0300280920660976 }, { -2.087047158406722, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -0.5441892438806311, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, -1.1448057498013176, 0.8389893256787506, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, -0.5143892000079515, 0.8671492160299267 }, { -0.8403433999584713, 0.14012794477794924, -0.19824982751343181, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 2.2929925029884224, 2.065822227902576, -0.9551523664354273, -0.94101658068771, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 1.4284264144452141, 1.0830488202002277, -0.8771690980243297 }, { -0.8403433999584713, 0.9532703900465632, 1.3730492931881484, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, -1.0459524004866725, 0.6837776537937139, -2.3724904782364917, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -0.7171415877086972, -1.0899191131053325, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, -1.4861569457592787, -0.9838993878642219, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.970316152682646, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.1833753304309906, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, -1.4810169520224028, -1.4653525684453887, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.14012794477794924, 1.3730492931881484, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, -0.250470603921817, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, -1.4810169520224028, -1.4653525684453887, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.970316152682646, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, 0.8389893256787506, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, -0.5143892000079515, -0.2957296600062443 }, { -0.8403433999584713, 0.14012794477794924, -0.5910746076888268, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, -0.250470603921817, -2.9105462303202203, -0.2957296600062443 }, { 1.029712237713905, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.1833753304309906, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.14012794477794924, -0.5910746076888268, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, 1.7814601887614623, 0.6837776537937139, -2.3724904782364917, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, -0.250470603921817, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -0.5441892438806311, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, 0.8389893256787506, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, -0.5143892000079515, -0.2957296600062443 }, { -0.8403433999584713, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.970316152682646, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, 1.7658740733635434, 1.185916567160356, -0.5441892438806311, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, 1.0087021598534562, 0.2843298100961381, -0.8771690980243297 }, { -0.21699152073434588, 0.9532703900465632, -1.376724168039617, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, -0.94101658068771, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, 0.28570977801184116 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.970316152682646, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.2289977647710673, -1.1448057498013176, 0.8389893256787506, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, -0.5143892000079515, -0.2957296600062443 }, { -0.8403433999584713, 0.9532703900465632, 1.3730492931881484, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -0.5114723838114789, -1.1448057498013176, -1.0459524004866725, 0.6837776537937139, -2.3724904782364917, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -0.7171415877086972, -1.0899191131053325, 1.0830488202002277, -0.8771690980243297 }, { -0.21699152073434588, 0.14012794477794924, -0.19824982751343181, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 2.2929925029884224, 2.065822227902576, -0.9551523664354273, -0.94101658068771, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 1.4284264144452141, 1.0830488202002277, -0.8771690980243297 }, { 1.029712237713905, 0.9532703900465632, 1.3730492931881484, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -0.5114723838114789, -1.1448057498013176, -1.0459524004866725, 0.6837776537937139, -2.3724904782364917, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -0.7171415877086972, -1.0899191131053325, 1.0830488202002277, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -0.5441892438806311, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, -1.1448057498013176, 0.8389893256787506, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, -0.5143892000079515, 0.8671492160299267 }, { -0.8403433999584713, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.1833753304309906, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -2.087047158406722, 0.14012794477794924, 1.3730492931881484, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -1.0459524004866725, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 1.4284264144452141, -1.313108210112041, -0.2957296600062443 }, { 1.029712237713905, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, -1.4653525684453887, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, 1.7658740733635434, 1.185916567160356, -0.5441892438806311, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.9532703900465632, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.05347685426934428, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, -1.4861569457592787, -0.19824982751343181, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, -1.4810169520224028, -1.4653525684453887, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, -0.5910746076888268, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, 0.2843298100961381, -0.2957296600062443 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.5114723838114789, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, 0.2843298100961381, -0.2957296600062443 }, { -0.8403433999584713, 0.9532703900465632, -0.5910746076888268, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, -3.0383605317184252, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, 0.881937664921384, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, 2.0300280920660976 }, { 1.029712237713905, -1.4861569457592787, 1.3730492931881484, -0.8432296400028142, -1.4949405164153078, 0.16289645171177966, 2.278612115023465, 1.494682719884397, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, -0.5143892000079515, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, 0.881937664921384, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, 0.05347685426934428, -1.1448057498013176, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, -0.2957296600062443 }, { -0.8403433999584713, 0.14012794477794924, -0.5910746076888268, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.5114723838114789, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, 0.2843298100961381, -0.2957296600062443 }, { -0.8403433999584713, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, 0.2843298100961381, -0.2957296600062443 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, -0.8432296400028142, -1.4949405164153078, 0.16289645171177966, 2.278612115023465, 1.494682719884397, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, -0.5143892000079515, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, -0.5910746076888268, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.5114723838114789, 0.8735106372181927, -0.10348153740396093, -2.534652044595703, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, -0.5143892000079515, -0.2957296600062443 }, { -2.087047158406722, 0.9532703900465632, 1.3730492931881484, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, 1.4284264144452141, -2.1118272202161306, 0.28570977801184116 }, { -0.21699152073434588, 0.9532703900465632, 1.7658740733635434, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.7483245685118138, -1.1448057498013176, 0.8389893256787506, 2.2929925029884224, 2.065822227902576, 1.1483059759124745, 1.156327370343005, 0.0, 4.263128144400531, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, -2.1118272202161306, 0.28570977801184116 }, { -0.8403433999584713, 0.9532703900465632, -0.19824982751343181, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -1.3588962409327137, 0.8735106372181927, -1.0459524004866725, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, 1.448588654048012 }, { -0.8403433999584713, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, -1.4861569457592787, 0.1945749526619632, -0.8432296400028142, -1.4949405164153078, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, 1.1833753304309906, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.14012794477794924, 0.1945749526619632, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 0.5889779052616985, 0.2843298100961381, 0.8671492160299267 }, { 1.029712237713905, 0.14012794477794924, -0.19824982751343181, 1.185916567160356, 0.881937664921384, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -0.2289977647710673, -1.1448057498013176, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.6701948585135749, 0.2843298100961381, 2.0300280920660976 }, { -0.8403433999584713, 0.9532703900465632, 1.7658740733635434, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.14012794477794924, -0.19824982751343181, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -1.3588962409327137, 0.8735106372181927, -1.0459524004866725, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, 1.448588654048012 }, { -0.8403433999584713, 0.9532703900465632, -0.9838993878642219, -0.8432296400028142, 1.3573133011887224, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -1.3588962409327137, 0.8735106372181927, -1.0459524004866725, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.9532703900465632, -0.5910746076888268, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, 0.14012794477794924, 0.1945749526619632, -0.8432296400028142, -1.4949405164153078, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.1833753304309906, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.14012794477794924, -0.5910746076888268, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, 0.2843298100961381, -0.2957296600062443 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.5114723838114789, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, -2.3724904782364917, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, -0.8432296400028142, -1.4949405164153078, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.1833753304309906, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, 0.9532703900465632, -0.9838993878642219, -0.8432296400028142, 1.3573133011887224, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -1.3588962409327137, 0.8735106372181927, -1.0459524004866725, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, 0.2843298100961381, -0.8771690980243297 }, { -0.8403433999584713, 0.9532703900465632, 0.1945749526619632, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 0.5889779052616985, 0.2843298100961381, 0.8671492160299267 }, { -0.8403433999584713, 0.14012794477794924, 0.1945749526619632, 1.185916567160356, 0.40656202865404567, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, 3.4332552519568216, 0.9480808566164142, 0.5889779052616985, 0.2843298100961381, 0.8671492160299267 }, { -0.8403433999584713, -1.4861569457592787, -0.5910746076888268, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, -0.9551523664354273, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, -1.4861569457592787, 1.3730492931881484, -0.8432296400028142, -1.4949405164153078, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, -0.5143892000079515, -0.8771690980243297 }, { -0.8403433999584713, -1.4861569457592787, -0.5910746076888268, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 0.6184260923501674, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.5114723838114789, 0.8735106372181927, -0.10348153740396093, -2.534652044595703, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, -0.5143892000079515, -0.2957296600062443 }, { -0.8403433999584713, 0.14012794477794924, -0.5910746076888268, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, 1.4658499494714023, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, 0.2843298100961381, -0.2957296600062443 }, { -2.087047158406722, 0.9532703900465632, 1.7658740733635434, -0.8432296400028142, 0.40656202865404567, 0.16289645171177966, 2.278612115023465, 1.494682719884397, 1.7483245685118138, -1.1448057498013176, 0.8389893256787506, 2.2929925029884224, 2.065822227902576, 1.1483059759124745, 1.156327370343005, 0.0, 4.263128144400531, -0.2561317410190009, -1.2722157358170676, 1.4284264144452141, -2.1118272202161306, 0.28570977801184116 }, { 1.029712237713905, 0.14012794477794924, 1.3730492931881484, 1.185916567160356, 0.881937664921384, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, -0.2289977647710673, -1.1448057498013176, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, 2.0300280920660976 }, { -0.8403433999584713, 0.9532703900465632, -0.5910746076888268, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, 0.14012794477794924, -0.5910746076888268, 1.185916567160356, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.5114723838114789, 0.8735106372181927, -0.10348153740396093, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -1.0899191131053325, -0.5143892000079515, -0.2957296600062443 }, { 1.029712237713905, -1.4861569457592787, -0.5910746076888268, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.5114723838114789, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, 0.09657680473852359, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 }, { -0.8403433999584713, -1.4861569457592787, 1.7658740733635434, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, -0.9551523664354273, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 }, { 1.029712237713905, 0.14012794477794924, -0.19824982751343181, 1.185916567160356, 0.881937664921384, 0.16289645171177966, -0.4388636369510842, 1.494682719884397, 1.4658499494714023, -1.1448057498013176, 1.7814601887614623, 0.6837776537937139, 0.5863846591895536, 0.622441390325499, 0.6319913825853262, 0.0, 0.14203663498716684, -0.2561317410190009, 0.9480808566164142, -0.250470603921817, 0.2843298100961381, 2.0300280920660976 }, { -0.8403433999584713, 0.9532703900465632, 1.7658740733635434, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, -0.9551523664354273, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, -0.8771690980243297 }, { 1.029712237713905, -1.4861569457592787, 1.7658740733635434, -0.8432296400028142, -1.0195648801479693, 0.16289645171177966, -0.4388636369510842, -0.6690383093994308, -0.7939470028518905, -1.1448057498013176, -0.10348153740396093, -0.9254371954009945, -0.893052909523469, -3.058610708783329, 0.10765539482764744, 0.0, 0.14203663498716684, -0.2561317410190009, -0.16206743960032674, -1.0899191131053325, 0.2843298100961381, 1.448588654048012 } };
            double[,] coefficients_mashroom_d	= { { -1.3798597708867266, -4.883607955379984, -5.548165790648151, -0.061757313767708176, -1.0865719010661703, -0.01426424141400819, -0.6163936088063474, -4.260153928818506, -1.2569041780574652, -2.1466027594616666, -2.2687652579772193, -6.118469300074238, -0.4924357275715736, -19.86696648294654, -0.34593690639143176, -1.0568472890871559, -0.14068891807316228, -0.9774166529477862, -7.130533106146039, -1.4840676028542605, -1.7639384438998986, -1.2819441836162062, -5.406881303996927, -0.9428957399138065, -2.322454934072298, -1.256062020922827, -2.860208657375192, -8.666078918366267, -0.4259696231740045, -0.473397647886441, -0.573640257921649, -0.16074059078640462, -6.261905222713596, -0.2049332572451397, -1.424903904529542, -0.12479587490241022, -8.958852855238515, -0.12189667565667259, -0.24280512531928305, -8.553587621769204, -3.292456463251167, -0.1290079928645057, -1.5624125899701915, -1.4162751660162036, -9.559136292753655, -5.651622437034512, -9.154899613658696, -0.6599745540188126, -0.07990402107507785, -5.260578627046839, -2.724836345832232, -13.289853100018485, -0.9342464605060691, 9.909437436603506, 11.101371474384855, 14.60593660919845, 0.5929858932655261, 2.397355266695858, 1.4186969140985455, 2.2551230051915527, 13.044814491403326, 2.537949671413006, 0.09808894794076044, 0.451166340737226, 2.9190463941871516, 0.2879197684262461, 2.5310436365072775, 6.335447288910088, 0.46137414846171526, 2.5343253229422906, 1.192585411419088, 1.6910118124713702, 7.046825275416602, 1.88315353921033, 0.19950359763170292, 4.279542727719369, 0.15618881292275336, 4.969094934430523, 6.899153231002094, 0.2435028745144476, 22.331397870782144, 8.619190232607641, 1.9735084693990874, 2.0925254204676658, 1.7430664187590996, 3.363379375281906, 0.7355121005045911, 0.19870584895371457, 3.306725180785799, 3.834438723728394, 1.0693253739909567, 3.596272507907274, 4.818436678576192, 2.1717114699735163, 4.046615221790625, 0.936049495114594 } };
            double[] intercepts_mashroom		= { -0.3209481162941494 };
            int[] weights_mashroom				= { 53, 43 };


            double[][] vectors_mashroom			= ExtensionMethods.ToJaggedArray(vectors_mashroom_d);
            double[][] coefficients_mashroom	= ExtensionMethods.ToJaggedArray(coefficients_mashroom_d);



            var bytes_mashrooms = Properties.Resources.mashrooms;
            numOfRows = 0;
            features = LoadFeatures(bytes_mashrooms, vectors_mashroom[0].Length, ref numOfRows);

            numOfRows = 5;

            if (RunSvc)
            {
                Console.WriteLine("SVC Mashroom: ");
                Stopwatch constructorPredictSum = new Stopwatch();
                constructorPredictSum.Start();
                SVC clf = new SVC(2, 2, vectors_mashroom, coefficients_mashroom, intercepts_mashroom, weights_mashroom, "poly", 0.045454545454545456, 0.0, 2);
                constructorPredictSum.Stop();
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(
                        $@"{OutputDir}SVMBatchPoly_RunSvc_Mashroom_classification_result.txt")
                )
                {
                    Stopwatch timePredictSum = new Stopwatch();
                    for (int i = 0; i < numOfRows; i++)
                    {
                        timePredictSum.Start();
                        int estimation = clf.Predict(features[i], out var finalResult);
                        timePredictSum.Stop();
                        Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
                        Console.WriteLine($"SVC estimation{i} is : {estimation} ");
                        file.WriteLine($"SVC estimation{i} is : {estimation} finalResult = {finalResult}");
                        Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
                    }
                    int avgPredict = (int)(timePredictSum.Elapsed.TotalMilliseconds / numOfRows);
                    Console.WriteLine($"Constructor: {constructorPredictSum.Elapsed.TotalMilliseconds} ms");
                    Console.WriteLine($"Average Predict: {avgPredict} ms");
                    Console.WriteLine($"Total time Predict: {timePredictSum.Elapsed.TotalMilliseconds} ms");
                    file.WriteLine($"Constructor: {constructorPredictSum.Elapsed.TotalMilliseconds} ms");
                    file.WriteLine($"Average Predict: {avgPredict} ms");
                    file.WriteLine($"Total time Predict: {timePredictSum.Elapsed.TotalMilliseconds} ms");
                }
            }

     

            Console.WriteLine("\n\n SecureSVC Mashroom: ");
           // int scale = 40;
            SecureSVC clf_mashhroom =
                new SecureSVC(2, vectors_mashroom, coefficients_mashroom, intercepts_mashroom, weights_mashroom, "poly", 0.045454545454545456, 0.0, 2, scale);
            //IrisSVC clf = new IrisSVC( 2, vectors, coefficients, intercepts/*, weights, "poly"*/, 0.25, 0.0, 3);

            //bool useRelinearizeInplace = true;
            //bool useReScale = true;


            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(
                    $@"{OutputDir}SVMBatchPoly_IrisSecureSVC_MASHROOM_classification_result_{scale}_{useRelinearizeInplace}_{useReScale}.txt")
            )
            {
                Stopwatch timePredictSum = new Stopwatch();
                for (int i = 0; i < numOfRows; i++)
                {
                    Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
                    double finalResult = 0;
                    timePredictSum.Start();
                    int estimation = clf_mashhroom.Predict(features[i], useRelinearizeInplace, useReScale, out finalResult);
                    timePredictSum.Stop();
                    file.WriteLine($"SecureSVC estimation{i} is : {estimation}  finalResult = {finalResult}");
                    Console.WriteLine($"SecureSVC estimation{i} is : {estimation}  finalResult = {finalResult}");
                    Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
                }

                int avgPredict = (int)(timePredictSum.Elapsed.TotalMilliseconds / numOfRows);
                Console.WriteLine($"Average Predict: {avgPredict} ms");
                file.WriteLine($"Average Predict: {avgPredict} ms");
                file.WriteLine($"Total time Predict: {timePredictSum.Elapsed.TotalMilliseconds} ms");
            }


            Console.WriteLine("End , press Enter to quit");


			Console.ReadLine();

		}

		private static double[][] LoadFeatures(byte[] bytes,int numberOfFeatuters ,ref int numOfRows)
		{
			double[][] features;
			List<double[]> rows = new List<double[]>();
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

					for (int j = 0; j < numberOfFeatuters; j++)
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

			return features;
		}
	}
}

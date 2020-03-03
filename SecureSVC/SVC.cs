using Microsoft.Research.SEAL;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SecureSVC
{
    public class Svc
    {

        /// <summary>
        ///  Library class  for General plain SVM initialization and prediction
        /// </summary>
        

		// The secure svm version support only this kernels
        private enum Kernel
        {
            Linear,
            Poly,
        }

		//Support vectors of the svm
        private readonly double[][] _vectors;
        private readonly double[][] _coefficients;
        private readonly double[] _intercepts;
        private readonly Kernel _kernel;
        private readonly double _gamma;
        private readonly double _coef0;
        private readonly ulong _degree;

        //The power of the scale to be used , determines the calculations percision
        private readonly int _power;

        private readonly SEALContext _context;
        private readonly PublicKey _publicKey;
        private readonly SecretKey _secretKey;
        private readonly RelinKeys _relinKeys;
        private readonly GaloisKeys _galoisKeys;
        private readonly Evaluator _evaluator;
        private readonly Decryptor _decryptor;
        private readonly CKKSEncoder _encoder;

        //Number of Support Vectors
        private readonly int _numOfrowsCount;
        //Number of Support Features
        private readonly int _numOfcolumnsCount;
        // Support vectors plaintexts used in batch inner product calculation
        private readonly Plaintext[] _svPlaintexts;
        // intermidiate sums ciphertext
        private readonly Ciphertext[] _sums;
        // kernels  ciphertext
        private readonly Ciphertext[] _kernels;
        // decision arrays ciphertext
        private readonly Ciphertext[] _decisionsArr;
		//Plaintext for coef
        private readonly Plaintext[] _coefArr;
        //Plaintext for gamma that is used in polynimial kernel
        private readonly Plaintext _gamaPlaintext;
        // Support vectors plaintexts used in simple inner product calculation
        private Plaintext[,] _svPlaintextsArr;
        // Ciphertexts of inner product sums needed in  simple inner product calculation
        private Ciphertext[] _innerProdSums;
		// scale = 2^power 
        private double _scale;
		// encoding of zero causes eception , some svm parametrs like a index of svm vectors can be zero 
		// To avoid eception we use very small number that won't effect the calculation result
        private const double Zero = 1.0E-9;

		// Flags for debug or alggorithm selection purposes
        private const bool PRINT_SCALE = false;
        private const bool PrintExactScale = false;
        private const bool PrintCipherText = false;
        private const bool UseBatchInnerProduct = true;



        public Svc(double[][] vectors, double[][] coefficients, double[] intercepts, String kernel, double gamma,
	        double coef0, ulong degree, int power, PublicKey publicKey, RelinKeys relinKeys,
	        GaloisKeys galoisKeys, int batchSize, int featureSize) : this(vectors, coefficients, intercepts, kernel, gamma, coef0, degree, power, publicKey, null,relinKeys,galoisKeys,batchSize,featureSize)
        {
			
        }

        //constructor for debugging , because it enables to pass secretkey which will not happen in real life
        // special parameters :
        //  batchsize - number of batches sample in one ciphertext ,if the client batches mulitple  samples in the ciphertet .
        //  featureSize - number of features in sample
		
        private Svc(double[][] vectors, double[][] coefficients, double[] intercepts,String kernel, double gamma, double coef0, ulong degree, int power, PublicKey publicKey, SecretKey secretKey, RelinKeys relinKeys, GaloisKeys galoisKeys,int batchSize,int featureSize)
        {                                                                                                                                                                                  

            this._vectors = vectors;
            this._coefficients = coefficients;
            this._intercepts = intercepts;

            this._kernel = (Kernel)System.Enum.Parse(typeof(Kernel), kernel);
            this._gamma = gamma;
            this._coef0 = coef0;
            this._degree = degree;
            this._power = power;
			//Use the ckks SCheme
            EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);

            // polyModulusDegree and CoeffModulus used for general SVM algorithm and depends on the polynomial kernel degree
			// ( and the percision constraint ) . 
			// My implementation can be used up to dgree = 4 , but it can be easly refactored with some oprimizations  to higher 
			// polynomial degree
            ulong polyModulusDegree = 16384;

            if (power >= 20 && power < 40)
            {
                parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
                    new int[] { 60, 20, 21, 22, 23, 24, 25, 26, 27, 60 });
            }
            else if (power >= 40 && power < 60)
            {
                parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
                    new int[] { 60, 40, 40, 40, 40, 40, 40, 40, 60 });
            }
            else if (power == 60)
            {
                polyModulusDegree = 32768;
                parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
                    new int[] { 60, 60, 60, 60, 60, 60, 60, 60, 60 });
            }
            parms.PolyModulusDegree = polyModulusDegree;
            _context = new SEALContext(parms);



            _publicKey = publicKey;
            _secretKey = secretKey;
            _relinKeys = relinKeys;
	        _galoisKeys = galoisKeys;

            _evaluator = new Evaluator(_context);
			if(_secretKey != null)
				_decryptor = new Decryptor(_context, _secretKey); //FOR DEBUG ONLY ( not used in real server)
            _encoder = new CKKSEncoder(_context);


            Stopwatch serverInitStopwatch = new Stopwatch();
            serverInitStopwatch.Start();



            _numOfrowsCount = _vectors.Length;			//Number of Support Vectors
            _numOfcolumnsCount = _vectors[0].Length;	//Number of features in every Support vector
            _scale = Math.Pow(2.0, _power);


            //	vars for batch rotations
            _svPlaintexts = new Plaintext[_numOfrowsCount];

            //Encode support vectors
            _sums = new Ciphertext[_numOfrowsCount];
            if (UseBatchInnerProduct)
            {

                double[] batchVectors = new double[batchSize * featureSize];

                for (int i = 0; i < _numOfrowsCount; i++)
                {

                    for (int k = 0; k < batchSize * featureSize; k++)
                    {
                        var index0 = k % featureSize;

                        batchVectors[k] = index0 < _numOfcolumnsCount ? _vectors[i][index0] : 0;


                    }
                    _svPlaintexts[i] = new Plaintext();
                    _encoder.Encode(batchVectors, _scale, _svPlaintexts[i]);
                    PrintScale(_svPlaintexts[i], "batch supportVectorsPlaintext" + i);
                    _sums[i] = new Ciphertext();
                }
            }
            else
            {
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

                        _encoder.Encode(_vectors[i][j] != 0 ? _vectors[i][j] : Zero, _scale, _svPlaintextsArr[i, j]);
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

            }

            // Allocate memory for svm secure calculation
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
            _encoder.Encode(_gamma != 0 ? _gamma : Zero, _scale, _gamaPlaintext);


            serverInitStopwatch.Stop();
            Console.WriteLine($"server Init elapsed {serverInitStopwatch.ElapsedMilliseconds} ms");
        }


        // Function for classification of samples 
        // I follow the SEAL eamples recomandations to rescale and relinearize after each calculation.
        // useRelinearizeInplace and useReScale should be always true.
		// This parametrs are enabled only for debugging and learning purpose.
        public Ciphertext  Predict(    Ciphertext featuresCiphertexts, bool useRelinearizeInplace, bool useReScale,
	        Stopwatch innerProductStopwatch, Stopwatch degreeStopwatch, Stopwatch negateStopwatch, Stopwatch serverDecisionStopWatch)
        {

            Ciphertext tempCt = new Ciphertext();

            // Level 1
            for (int i = 0; i < _numOfrowsCount; i++)
            {

                //inner product
                //calculate IP = < x, x'>
                innerProductStopwatch.Start();
                if (UseBatchInnerProduct)
                {
                    _kernels[i] = InnerProduct(featuresCiphertexts, _svPlaintexts, i, _sums, _numOfcolumnsCount,tempCt);
                }


                innerProductStopwatch.Stop();
                PrintCyprherText(_decryptor, _kernels[i], _encoder, $"inner product TotalValue {i}");
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

				//For polynimial kernel calculate 
                if (_kernel == Kernel.Poly)
                {
                    // calculate (y *IP+r)^d
                    // IP is calculated previously
                    // y = gamma
                    // r = _coef0
                    if (useReScale)
                    {
                        ParmsId lastParmsId = _kernels[i].ParmsId;
                        _evaluator.ModSwitchToInplace(_gamaPlaintext, lastParmsId);
                    }
                    //calculate y * IP
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

					// add r
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
					// calculate the polynom degree
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

                PrintScale(_kernels[i], "8. kernel" + i);

                PrintCyprherText(_decryptor, _kernels[i], _encoder, "kernel" + i);

            }
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
                PrintScale(_coefArr[i], "coefPlainText" + i);
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

            double scale3 = Math.Pow(2.0, _power * 3);
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
            List<double> result = PrintCyprherText(_decryptor, decisionTotal, _encoder, "finalTotal", true);

            serverDecisionStopWatch.Stop();
            long innerProductMilliseconds = innerProductStopwatch.ElapsedMilliseconds;
            //Console.WriteLine($"server innerProductStopwatch elapsed {innerProductMilliseconds} ms");
            long negateMilliseconds = negateStopwatch.ElapsedMilliseconds;
            //Console.WriteLine($"server negateStopwatch elapsed {negateMilliseconds} ms");
            long degreeMilliseconds = degreeStopwatch.ElapsedMilliseconds;
            //Console.WriteLine($"server degreeStopwatch elapsed {degreeMilliseconds} ms");
            long serverDecisionMilliseconds = serverDecisionStopWatch.ElapsedMilliseconds;
            //Console.WriteLine($"server Decision elapsed {serverDecisionMilliseconds} ms");


            return decisionTotal;


        }

		//Function for calculating secured batched inner product
        private Ciphertext InnerProduct(Ciphertext featuresCiphertexts, Plaintext[] svPlaintexts, int i, Ciphertext[] sums, int numOfcolumnsCount, Ciphertext tempCt)
        {
            _evaluator.MultiplyPlain(featuresCiphertexts, svPlaintexts[i], sums[i]);
            int numOfRotations = (int)Math.Ceiling(Math.Log(numOfcolumnsCount,2));

            for (int k = 1, m = 1; m <= numOfRotations /*(int)encoder.SlotCount/2*/; k <<= 1, m++)
            {
                _evaluator.RotateVector(sums[i], k, _galoisKeys, tempCt);
                _evaluator.AddInplace(sums[i], tempCt);
            }

            var sum = sums[i];
            return sum;
        }

		//Function for conditionally printing the scale of the ciphertext for debug 
        private static void PrintScale(Ciphertext ciphertext, String name)
        {
            if (!PRINT_SCALE) return;
            if (PrintExactScale)
            {
                Console.Write($"    + Exact scale of {name}:");
                Console.WriteLine(" {0:0.0000000000}", ciphertext.Scale);
            }

            Console.WriteLine("    + Scale of {0}: {1} bits ", name,
                    Math.Log(ciphertext.Scale, newBase: 2)/*, _decryptor.InvariantNoiseBudget(ciphertext)*/);
        }

        //Function for conditionally printing the scale of the plaintext for debug 
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

        //Function for printing the cipher text for debug 
        private static List<double> PrintCyprherText(Decryptor decryptor, Ciphertext ciphertext, CKKSEncoder encoder, String name, bool print = false)
        {
            if (!PrintCipherText && !print) return null;
            if (decryptor == null) return null;
            Plaintext plainResult = new Plaintext();
            decryptor.Decrypt(ciphertext, plainResult);
            List<double> result = new List<double>();
            encoder.Decode(plainResult, result);

            Console.WriteLine($"{name} TotalValue = {result[0]}");
            return result;
        }


    }
}

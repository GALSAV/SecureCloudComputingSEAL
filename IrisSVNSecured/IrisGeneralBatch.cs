﻿using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace IrisSVMSecured
{
    class IrisGeneralBatch
    {
        private const bool RunSvc      = false;
        private const bool RunIrisSvc  = false;

        public class IrisSecureSvc
        {
            private enum Kernel
            {
                Linear,
                Poly,
                Rbf,
                Sigmoid
            }

            //private int _nClasses;
            private int _nRows;

            //private int[] classes;
            private double[][] _vectors;
            private double[][] _coefficients;
            private double[] _intercepts;
            private Kernel _kernel;
            private double _gamma;
            private double _coef0;
            private double _degree;

            private static bool _firstTime = true;
            //private static Decryptor _decryptor;

            public IrisSecureSvc(int nRows, double[][] vectors, double[][] coefficients, double[] intercepts,
                String kernel, double gamma, double coef0, double degree)
            {


                this._nRows = nRows;

                this._vectors = vectors;
                this._coefficients = coefficients;
                this._intercepts = intercepts;

                this._kernel = Enum.Parse<Kernel>(kernel, true);
                this._gamma = gamma;
                this._coef0 = coef0;
                this._degree = degree;
            }

            public int Predict(double[] features,int power, bool useRelinearizeInplace,bool useReScale)
            {
                EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);

                if (power < 60)
                {
                    ulong polyModulusDegree = 8192;
                    parms.PolyModulusDegree = polyModulusDegree;
                    parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree, new int[] {60, 40, 40, 60});
                }
                else
                {
                    ulong polyModulusDegree = 16384;
                    parms.PolyModulusDegree = polyModulusDegree;
                    parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree, new int[] { 60, 60, 60, 60, 60, 60 });
                }
                //

                double scale = Math.Pow(2.0, power);

                SEALContext context = new SEALContext(parms);
               
                Console.WriteLine();

                KeyGenerator keygen = new KeyGenerator(context);
                PublicKey publicKey = keygen.PublicKey;
                SecretKey secretKey = keygen.SecretKey;
                RelinKeys relinKeys = keygen.RelinKeys();
                var galoisKeys = keygen.GaloisKeys();
                Encryptor encryptor = new Encryptor(context, publicKey);
                Evaluator evaluator = new Evaluator(context);
                Decryptor decryptor = new Decryptor(context, secretKey);

                CKKSEncoder encoder = new CKKSEncoder(context);

                ulong slotCount = encoder.SlotCount;
                
                Console.WriteLine($"Number of slots: {slotCount}");

                var featuresLength = features.Length;





                var plaintexts  = new Plaintext();
                var featuresCiphertexts = new Ciphertext();


                //Encode and encrypt features

                encoder.Encode(features, scale, plaintexts);
                encryptor.Encrypt(plaintexts, featuresCiphertexts);
                PrintScale(plaintexts, "featurePlaintext");
                PrintScale(featuresCiphertexts, "featurefEncrypted");



                // Handle SV
                var numOfrows    = _vectors.Length;
                var numOfcolumns = _vectors[0].Length;

                int numOfRotations = (int)Math.Ceiling(Math.Log2(numOfcolumns));
                //Console.WriteLine("**********************************    : "+ numOfRotations);
                var svPlaintexts = new Plaintext[numOfrows];

                //Encode SV
                for (int i = 0; i < numOfrows; i++)
                {
                    //for (int j = 0; j < numOfcolumns; j++)
                    //{
                        svPlaintexts[i] = new Plaintext();
                        encoder.Encode(_vectors[i], scale, svPlaintexts[i]);
                        PrintScale(svPlaintexts[i], "supportVectorsPlaintext"+i);
                    //}
                }
                // Prepare sum of inner product
                var sums = new Ciphertext[numOfcolumns];
                for (int i = 0; i < numOfcolumns; i++)
                {
                    sums[i] = new Ciphertext();
                }

                var kernels      = new Ciphertext[numOfrows];
                var decisionsArr = new Ciphertext[numOfrows];
                var coefArr      = new Plaintext [numOfrows];

                for (int i = 0; i < numOfrows; i++)
                {
                    kernels[i]       = new Ciphertext();
                    decisionsArr[i]  = new Ciphertext();
                    coefArr[i]       = new Plaintext();
                }

               
                // Level 1
                for (int i = 0; i < numOfrows; i++)
                {
                    var ciphertexts = new List<Ciphertext>();

                    evaluator.MultiplyPlain(featuresCiphertexts, svPlaintexts[i],sums[i]);
                    
                    for (int k = 1; k <= numOfRotations+1/*(int)encoder.SlotCount*/ / 2; k <<= 1)
                    {
                        Ciphertext tempCt = new Ciphertext();
                        evaluator.RotateVector(sums[i], k, galoisKeys, tempCt);
                        evaluator.AddInplace(sums[i], tempCt);

                    }

                   
                    kernels[i] = sums[i];


                    evaluator.NegateInplace(kernels[i]);

                    if (useRelinearizeInplace)
                    {
                        evaluator.RelinearizeInplace(kernels[i], relinKeys);
                    }

                    if (useReScale)
                    {
                        evaluator.RescaleToNextInplace(kernels[i]);
                    }

                    PrintScale(kernels[i], "kernel"+i); 

                    PrintCyprherText(decryptor, kernels[i], encoder, "kernel"+i);

                }

                // Encode coefficients : ParmsId! , scale!
                double scale2 = Math.Pow(2.0, power);
                if (useReScale)
                {
                    scale2 = kernels[0].Scale;
                }

                for (int i = 0; i < numOfrows; i++)
                {
                    encoder.Encode(_coefficients[0][i], scale2, coefArr[i]);
                    PrintScale(coefArr[i], "coefPlainText+i");
                }



                if (useReScale)
                {
                    for (int i = 0; i < numOfrows; i++)
                    {
                        ParmsId lastParmsId = kernels[i].ParmsId;
                        evaluator.ModSwitchToInplace(coefArr[i], lastParmsId);
                    }
                }
                // Level 2
                // Calculate decisionArr
                for (int i = 0; i < numOfrows; i++)
                {
                    evaluator.MultiplyPlain(kernels[i], coefArr[i], decisionsArr[i]);
                    if (useRelinearizeInplace)
                    {
                        evaluator.RelinearizeInplace(decisionsArr[i], relinKeys);
                    }

                    if (useReScale)
                    {
                        evaluator.RescaleToNextInplace(decisionsArr[i]);
                    }
                    PrintScale(decisionsArr[i], "decision"+i);
                    PrintCyprherText(decryptor, decisionsArr[i], encoder, "decision" + i);
                }



                // Calculate decisionTotal
                Ciphertext decisionTotal = new Ciphertext();
                //=================================================================
                evaluator.AddMany(decisionsArr, decisionTotal);
                //=================================================================
              
                PrintScale(decisionTotal, "decisionTotal"); 
                PrintCyprherText(decryptor, decisionTotal, encoder, "decision total");


                // Encode intercepts : ParmsId! , scale!
                Plaintext interceptsPlainText = new Plaintext();
                
                double scale3 = Math.Pow(2.0, power*3);
                if (useReScale)
                {
                    scale3 = decisionTotal.Scale;
                }
                encoder.Encode(_intercepts[0], scale3, interceptsPlainText);
                if (useReScale)
                {
                    ParmsId lastParmsId = decisionTotal.ParmsId;
                    evaluator.ModSwitchToInplace(interceptsPlainText, lastParmsId);
                }

                PrintScale(interceptsPlainText, "interceptsPlainText");
                PrintScale(decisionTotal, "decisionTotal");


                //// Calculate finalTotal
                Ciphertext finalTotal = new Ciphertext();

                //=================================================================
                evaluator.AddPlainInplace(decisionTotal, interceptsPlainText);
                //=================================================================

                PrintScale(decisionTotal, "decisionTotal");  //Level 3
                List<double> result = PrintCyprherText(decryptor, decisionTotal, encoder, "finalTotal");
                
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(
                        $@"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\GeneralBatch_IrisSecureSVC_total_{power}_{useRelinearizeInplace}_{useReScale}.txt", !_firstTime)
                )
                {
                    _firstTime = false;
                    file.WriteLine($"{result[0]}");
                }

                if (result[0] > 0)
                {
                    return 0;
                }

                return 1;

            }

            private static void PrintScale(Ciphertext ciphertext,String name)
            {
                Console.Write($"    + Exact scale of {name}:");
                Console.WriteLine(" {0:0.0000000000}", ciphertext.Scale);
                Console.WriteLine("    + Scale of {0}: {1} bits ", name,
                    Math.Log(ciphertext.Scale, newBase: 2)/*, _decryptor.InvariantNoiseBudget(ciphertext)*/);
            }

            private static void PrintScale(Plaintext plaintext, String name)
            {
                Console.Write($"    + Exact scale of {name}:");
                Console.WriteLine(" {0:0.0000000000}", plaintext.Scale);
                Console.WriteLine("    + Scale of {0}: {1} bits", name,
                    Math.Log(plaintext.Scale, newBase: 2));
            }

            private static List<double> PrintCyprherText(Decryptor decryptor, Ciphertext ciphertext, CKKSEncoder encoder,String name)
            {
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
                //var path = @"D:\GAL\Workspace\SecureCloudComputing\svm\com\data\iris.data";
                var bytes = Properties.Resources.iris;
                numOfRows = 0;
                //int numOfColums = 0;
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
                        //numOfColums = readFields.Length;

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

                    features[i] = rows[i]; //new double[numOfColums];
                }
            }


            double[][] vectors = new double[3][];

            vectors[0] = new[] {5.1, 3.3, 1.7, 0.5};
            vectors[1] = new[] {4.8, 3.4, 1.9, 0.2};
            vectors[2] = new[] {5.1, 2.5, 3.0, 1.1};

            double[][] coefficients = new double[1][];
            coefficients[0] = new double[] {-0.7407784813992192, -0.0025023664254470897, 0.7432808478246663};
            double[] intercepts = {0.9055182807973224};


            Console.WriteLine("\n\n SecureSVC : ");
            IrisSecureSvc clf3 =
                new IrisSecureSvc(2, vectors, coefficients, intercepts, "linear", 0.25, 0.0, 3);
            int scale = 40;
            bool useRelinearizeInplace = true;
            bool useReScale = true;

            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(
                    $@"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\GeneralBatch_IrisSecureSVC_classification_result_{scale}_{useRelinearizeInplace}_{useReScale}.txt")
            )
            {
                Stopwatch timePredictSum = new Stopwatch();
                for (int i = 0; i < numOfRows; i++)
                {
                    Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");

                    timePredictSum.Start();
                    int estimation = clf3.Predict(features[i],scale,useRelinearizeInplace,useReScale);
                    timePredictSum.Stop();
                    file.WriteLine($"IrisSecureSVC estimation{i} is : {estimation} ");
                    Console.WriteLine($"IrisSecureSVC estimation{i} is : {estimation} ");
                    Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
                }
                
                int avgPredict = (int)(timePredictSum.Elapsed.TotalMilliseconds * 1000 / numOfRows);
                Console.WriteLine($"Average Predict: {avgPredict} microseconds");
                file.WriteLine($"Average Predict: {avgPredict} microseconds");
            }


            Console.WriteLine("End , press Enter to quit");


            Console.ReadLine();

        }
    }
}

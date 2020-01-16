using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace IrisSVNSecured
{
    class IrisGeneralBatch
    {
        private const bool RunSvc      = false;
        private const bool RunIrisSvc  = false;


        public class SVC
        {
            private enum Kernel
            {
                Linear,
                Poly,
                Rbf,
                Sigmoid
            }

            private int _nClasses;
            private int nRows;
            private int[] classes;
            private double[][] vectors;
            private double[][] coefficients;
            private double[] intercepts;
            private int[] weights;
            private Kernel kernel;
            private double gamma;
            private double coef0;
            private double degree;

            public SVC(int nClasses, int nRows, double[][] vectors, double[][] coefficients, double[] intercepts,
                int[] weights, String kernel, double gamma, double coef0, double degree)
            {


                Console.WriteLine($"Create SVC with nClasses = {nClasses} , ");
                this._nClasses = nClasses;
                this.classes = new int[nClasses];
                for (int i = 0; i < nClasses; i++)
                {
                    this.classes[i] = i;
                }

                this.nRows = nRows;

                this.vectors = vectors;
                this.coefficients = coefficients;
                this.intercepts = intercepts;
                this.weights = weights;

                this.kernel = Enum.Parse<Kernel>(kernel, true);
                this.gamma = gamma;
                this.coef0 = coef0;
                this.degree = degree;
            }

            public int Predict(double[] features)
            {

                double[] kernels = new double[vectors.Length];
                double kernel;
                switch (this.kernel)
                {
                    case Kernel.Linear:
                        // <x,x'>
                        for (int i = 0; i < this.vectors.Length; i++)
                        {
                            kernel = 0;
                            for (int j = 0; j < this.vectors[i].Length; j++)
                            {
                                kernel += this.vectors[i][j] * features[j];
                                Console.WriteLine($"kernel += this.vectors[{i}][{j}] * features[{j}]");
                            }

                            kernels[i] = kernel;
                            Console.WriteLine($"kernels[{i}] = {kernel}");
                            Console.WriteLine("-----------------------------------------------------");
                        }

                        break;
                    case Kernel.Poly:
                        // (y<x,x'>+r)^d
                        for (int i = 0; i < this.vectors.Length; i++)
                        {
                            kernel = 0;
                            for (int j = 0; j < this.vectors[i].Length; j++)
                            {
                                kernel += this.vectors[i][j] * features[j];
                            }

                            kernels[i] = Math.Pow((this.gamma * kernel) + this.coef0, this.degree);
                        }

                        break;
                    case Kernel.Rbf:
                        // exp(-y|x-x'|^2)
                        for (int i = 0; i < this.vectors.Length; i++)
                        {
                            kernel = 0;
                            for (int j = 0; j < this.vectors[i].Length; j++)
                            {
                                kernel += Math.Pow(this.vectors[i][j] - features[j], 2);
                            }

                            kernels[i] = Math.Exp(-this.gamma * kernel);
                        }

                        break;
                    case Kernel.Sigmoid:
                        // tanh(y<x,x'>+r)
                        for (int i = 0; i < this.vectors.Length; i++)
                        {
                            kernel = 0;
                            for (int j = 0; j < this.vectors[i].Length; j++)
                            {
                                kernel += this.vectors[i][j] * features[j];
                            }

                            kernels[i] = Math.Tanh((this.gamma * kernel) + this.coef0);
                        }

                        break;
                }

                Console.WriteLine("Calculate weights : ");
                int[] starts = new int[this.nRows];
                for (int i = 0; i < this.nRows; i++)
                {
                    if (i != 0)
                    {
                        int start = 0;
                        for (int j = 0; j < i; j++)
                        {
                            start += this.weights[j];
                        }

                        starts[i] = start;
                        Console.WriteLine($"starts[{i}] = {start}");

                    }
                    else
                    {
                        starts[0] = 0;
                        Console.WriteLine($"starts[0] = 0");
                    }
                }

                int[] ends = new int[this.nRows];
                for (int i = 0; i < this.nRows; i++)
                {
                    ends[i] = this.weights[i] + starts[i];
                    Console.WriteLine($"ends[{i}] = this.weights[{i}] + starts[{i}]");
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
                        decision += kernels[k] * this.coefficients[0][k];
                        Console.WriteLine($"starts1 : decision += kernels[{k}] * this.coefficients[0][{k}]");
                        Console.WriteLine($"starts1 : decision = {decision}");
                    }

                    for (int k = starts[0]; k < ends[0]; k++)
                    {
                        decision += kernels[k] * this.coefficients[0][k];
                        Console.WriteLine($"starts0 : decision += kernels[{k}] * this.coefficients[0][{k}]");
                        Console.WriteLine($"starts0 : decision = {decision}");
                    }

                    Console.WriteLine($"Total decision = {decision}");
                    decision += this.intercepts[0];
                    
                    Console.WriteLine($"decision = {decision}");

                    if (decision > 0)
                    {
                        return 0;
                    }

                    return 1;

                }

                double[] decisions = new double[this.intercepts.Length];
                for (int i = 0, d = 0, l = this.nRows; i < l; i++)
                {
                    for (int j = i + 1; j < l; j++)
                    {
                        double tmp = 0;
                        for (int k = starts[j]; k < ends[j]; k++)
                        {
                            tmp += this.coefficients[i][k] * kernels[k];
                        }

                        for (int k = starts[i]; k < ends[i]; k++)
                        {
                            tmp += this.coefficients[j - 1][k] * kernels[k];
                        }

                        decisions[d] = tmp + this.intercepts[d];
                        d++;
                    }
                }

                int[] votes = new int[this.intercepts.Length];
                for (int i = 0, d = 0, l = this.nRows; i < l; i++)
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

                return this.classes[classIdx];

            }
        }



        public class IrisSVC
        {

            private int nRows;
            private double[][] vectors;
            private double[][] coefficients;
            private double[] intercepts;
            private double gamma;
            //private double coef0;
            private double degree;

            public IrisSVC( int nRows, double[][] vectors, double[][] coefficients, double[] intercepts, double gamma/*, double coef0*/, double degree)
            {


                this.nRows = nRows;

                this.vectors = vectors;
                this.coefficients = coefficients;
                this.intercepts = intercepts;

                this.gamma = gamma;
                //this.coef0 = coef0;
                this.degree = degree;
            }

            public int Predict(double[] features)
            {

                double[] kernels = new double[vectors.Length];
                double kernel;
                for (int i = 0; i < this.vectors.Length; i++)
                {
                    kernel = 0;
                    Console.WriteLine($"this.vectors[i].Length = {this.vectors[i].Length}");
                    for (int j = 0; j < this.vectors[i].Length; j++)
                    {
                        kernel += this.vectors[i][j] * features[j];
                        Console.WriteLine($"kernel += this.vectors[{i}][{j}] * features[{j}]");
                        Console.WriteLine($"kernel ( {kernel} )   += this.vectors[{i}][{j}] ({this.vectors[i][j]})  * features[{j}] ({features[j]})");
                    }
                    Console.WriteLine(@"-------------------------------------------------------------------------");
                    kernels[i] = kernel; //Math.Pow((this.gamma * kernel) + this.coef0, this.degree);
                    Console.WriteLine($@"kernels[{i}] = kernel ({kernel})");
                    Console.WriteLine(@"-----------------------------------------------------");
                }

                double decision = 0;
                for (int i = 0; i < kernels.Length; i++)
                {
                    decision += -kernels[i] * this.coefficients[0][i];
                    Console.WriteLine($" decision ({decision}) += -kernels[{i}]  ({kernels[i]}) * this.coefficients[0][{i}] ({this.coefficients[0][i]})");
                }

                Console.WriteLine($"Total decision {decision}");
                decision += this.intercepts[0];
                Console.WriteLine($"Final decision {decision}");

                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(
                        @"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\IrisSVC_total.txt", true)
                )
                {
                    file.WriteLine($"{decision}");
                }

                if (decision > 0)
                {
                    return 0;
                }

                return 1;
            }

        }



        public class IrisSecureSVC
        {
            private enum Kernel
            {
                Linear,
                Poly,
                Rbf,
                Sigmoid
            }

            //private int _nClasses;
            private int nRows;

            //private int[] classes;
            private double[][] vectors;
            private double[][] coefficients;
            private double[] intercepts;
            private int[] weights;
            private Kernel kernel;
            private double gamma;
            private double coef0;
            private double degree;

            private static bool firstTime = true;
            //private static Decryptor _decryptor;

            public IrisSecureSVC(int nRows, double[][] vectors, double[][] coefficients, double[] intercepts,
                int[] weights, String kernel, double gamma, double coef0, double degree)
            {


                this.nRows = nRows;

                this.vectors = vectors;
                this.coefficients = coefficients;
                this.intercepts = intercepts;
                this.weights = weights;

                this.kernel = Enum.Parse<Kernel>(kernel, true);
                this.gamma = gamma;
                this.coef0 = coef0;
                this.degree = degree;
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


                //for (int i = 0; i < featuresLength; i++)
                //{
                //    plaintexts[i] = new Plaintext();
                    
                //    encoder.Encode(features, scale, plaintexts);
                    
                //    PrintScale(plaintexts[i], "featurePlaintext" + i);
                //    featuresCiphertexts[i] = new Ciphertext();
                   
                //    encryptor.Encrypt(plaintexts[i], featuresCiphertexts[i]);
                //    PrintScale(featuresCiphertexts[i], "featurefEncrypted" + i);
                //}

                // Handle SV
                var numOfrows    = vectors.Length;
                var numOfcolumns = vectors[0].Length;

                int numOfRotations = (int)Math.Ceiling(Math.Log2(numOfcolumns));
                //Console.WriteLine("**********************************    : "+ numOfRotations);
                var svPlaintexts = new Plaintext[numOfrows];

                //Encode SV
                for (int i = 0; i < numOfrows; i++)
                {
                    //for (int j = 0; j < numOfcolumns; j++)
                    //{
                        svPlaintexts[i] = new Plaintext();
                        encoder.Encode(vectors[i], scale, svPlaintexts[i]);
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
                        //Console.WriteLine("######################   : " +k);

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
                    encoder.Encode(coefficients[0][i], scale2, coefArr[i]);
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
                encoder.Encode(intercepts[0], scale3, interceptsPlainText);
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
                        $@"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\GeneralBatch_IrisSecureSVC_total_{power}_{useRelinearizeInplace}_{useReScale}.txt", !firstTime)
                )
                {
                    firstTime = false;
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

                Console.WriteLine($"{name} result = {result[0]}");
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

                    //features; = new double[numOfcalsiffication][];
                    // Skip the row with the column names
                    //csvParser.ReadLine();


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
            int[] weights = {2, 1};

            if (RunSvc)
            {
                Console.WriteLine("SVC : ");
                SVC clf = new SVC(2, 2, vectors, coefficients, intercepts, weights, "linear", 0.25, 0.0, 3);
                //IrisSVC clf = new IrisSVC( 2, vectors, coefficients, intercepts/*, weights, "poly"*/, 0.25, 0.0, 3);
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(
                        @"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\SVC.txt")
                )
                {
                    for (int i = 0; i < numOfRows; i++)
                    {
                        int estimation = clf.Predict(features[i]);
                        Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
                        Console.WriteLine($"SVC estimation{i} is : {estimation} ");
                        file.WriteLine($"SVC estimation{i} is : {estimation} ");
                        Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
                    }
                }
            }


            if (RunIrisSvc)
            {
                Console.WriteLine("\n\n IrisSVC : ");
                IrisSVC clf2 = new IrisSVC(2, vectors, coefficients, intercepts, 0.25, 3);
                //IrisSVC clf = new IrisSVC( 2, vectors, coefficients, intercepts/*, weights, "poly"*/, 0.25, 0.0, 3);
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(
                        @"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\IrisSVC.txt")
                )
                {
                    for (int i = 0; i < numOfRows; i++)
                    {
                        Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
                        int estimation = clf2.Predict(features[i]);

                        Console.WriteLine($"IrisSVC estimation{i} is : {estimation} ");
                        file.WriteLine($"IrisSVC estimation{i} is : {estimation} ");
                        Console.WriteLine($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ \n\n");
                    }
                }

            }


            Console.WriteLine("\n\n SecureSVC : ");
            IrisSecureSVC clf3 =
                new IrisSecureSVC(2, vectors, coefficients, intercepts, weights, "linear", 0.25, 0.0, 3);
            ;
            //IrisSVC clf = new IrisSVC( 2, vectors, coefficients, intercepts/*, weights, "poly"*/, 0.25, 0.0, 3);
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

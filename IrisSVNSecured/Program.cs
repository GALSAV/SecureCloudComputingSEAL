using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;

namespace IrisSVNSecured
{
    class Program
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
                //Boolean useRelinearizeInplace = true;
                EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);
                //ulong polyModulusDegree    = 16384;
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
                //Utilities.PrintParameters(context);
                Console.WriteLine();

                KeyGenerator keygen = new KeyGenerator(context);
                PublicKey publicKey = keygen.PublicKey;
                SecretKey secretKey = keygen.SecretKey;
                RelinKeys relinKeys = keygen.RelinKeys();
                Encryptor encryptor = new Encryptor(context, publicKey);
                Evaluator evaluator = new Evaluator(context);
                Decryptor decryptor = new Decryptor(context, secretKey);

                CKKSEncoder encoder = new CKKSEncoder(context);

                ulong slotCount = encoder.SlotCount;
                
                Console.WriteLine($"Number of slots: {slotCount}");

                Plaintext fPlaintext0 = new Plaintext();
                Plaintext fPlaintext1 = new Plaintext();
                Plaintext fPlaintext2 = new Plaintext();
                Plaintext fPlaintext3 = new Plaintext();


                encoder.Encode(features[0], scale, fPlaintext0);
                encoder.Encode(features[1], scale, fPlaintext1);
                encoder.Encode(features[2], scale, fPlaintext2);
                encoder.Encode(features[3], scale, fPlaintext3);


                PrintScale(fPlaintext0, "fPlaintext0");
                PrintScale(fPlaintext1, "fPlaintext1");
                PrintScale(fPlaintext2, "fPlaintext2");

                Ciphertext f0Encrypted = new Ciphertext();
                Ciphertext f1Encrypted = new Ciphertext();
                Ciphertext f2Encrypted = new Ciphertext();
                Ciphertext f3Encrypted = new Ciphertext();
                encryptor.Encrypt(fPlaintext0, f0Encrypted);
                encryptor.Encrypt(fPlaintext1, f1Encrypted);
                encryptor.Encrypt(fPlaintext2, f2Encrypted);
                encryptor.Encrypt(fPlaintext3, f3Encrypted);

                PrintScale(f0Encrypted, "f0Encrypted");
                PrintScale(f1Encrypted, "f1Encrypted");
                PrintScale(f2Encrypted, "f2Encrypted");

                Plaintext v00Plaintext1 = new Plaintext();
                Plaintext v01Plaintext1 = new Plaintext();
                Plaintext v02Plaintext1 = new Plaintext();
                Plaintext v03Plaintext1 = new Plaintext();
                Plaintext v10Plaintext1 = new Plaintext();
                Plaintext v11Plaintext1 = new Plaintext();
                Plaintext v12Plaintext1 = new Plaintext();
                Plaintext v13Plaintext1 = new Plaintext();

                Plaintext v20Plaintext1 = new Plaintext();
                Plaintext v21Plaintext1 = new Plaintext();
                Plaintext v22Plaintext1 = new Plaintext();
                Plaintext v23Plaintext1 = new Plaintext();


                encoder.Encode(vectors[0][0], scale, v00Plaintext1);
                encoder.Encode(vectors[0][1], scale, v01Plaintext1);
                encoder.Encode(vectors[0][2], scale, v02Plaintext1);
                encoder.Encode(vectors[0][3], scale, v03Plaintext1);
                encoder.Encode(vectors[1][0], scale, v10Plaintext1);
                encoder.Encode(vectors[1][1], scale, v11Plaintext1);
                encoder.Encode(vectors[1][2], scale, v12Plaintext1);
                encoder.Encode(vectors[1][3], scale, v13Plaintext1);
                encoder.Encode(vectors[2][0], scale, v20Plaintext1);
                encoder.Encode(vectors[2][1], scale, v21Plaintext1);
                encoder.Encode(vectors[2][2], scale, v22Plaintext1);
                encoder.Encode(vectors[2][3], scale, v23Plaintext1);


                PrintScale(v00Plaintext1, "v00Plaintext1");
                PrintScale(v01Plaintext1, "v01Plaintext1");
                PrintScale(v02Plaintext1, "v02Plaintext1");
                PrintScale(v03Plaintext1, "v03Plaintext1");

                PrintScale(v10Plaintext1, "v10Plaintext1");
                PrintScale(v11Plaintext1, "v11Plaintext1");
                PrintScale(v12Plaintext1, "v12Plaintext1");
                PrintScale(v13Plaintext1, "v13Plaintext1");

                PrintScale(v20Plaintext1, "v20Plaintext1");
                PrintScale(v21Plaintext1, "v21Plaintext1");
                PrintScale(v22Plaintext1, "v22Plaintext1");
                PrintScale(v23Plaintext1, "v23Plaintext1");

                Plaintext coef00PlainText = new Plaintext();
                Plaintext coef01PlainText = new Plaintext();
                Plaintext coef02PlainText = new Plaintext();


                Ciphertext tSum1 = new Ciphertext();
                Ciphertext tSum2 = new Ciphertext();
                Ciphertext tSum3 = new Ciphertext();
                Ciphertext tSum4 = new Ciphertext();
                Ciphertext kernel0 = new Ciphertext();

                //Level 1->2
                //=================================================================
                evaluator.MultiplyPlain(f0Encrypted, v00Plaintext1, tSum1);
                evaluator.MultiplyPlain(f1Encrypted, v01Plaintext1, tSum2);
                evaluator.MultiplyPlain(f2Encrypted, v02Plaintext1, tSum3);
                evaluator.MultiplyPlain(f3Encrypted, v03Plaintext1, tSum4);
                //=================================================================
                
                if (useRelinearizeInplace)
                {
                    Console.WriteLine("RelinearizeInplace sums 1");
                    evaluator.RelinearizeInplace(tSum1, relinKeys);
                    evaluator.RelinearizeInplace(tSum2, relinKeys);
                    evaluator.RelinearizeInplace(tSum3, relinKeys);
                    evaluator.RelinearizeInplace(tSum4, relinKeys);
                }

                if (useReScale)
                {
                    Console.WriteLine("useReScale sums 1");
                    evaluator.RescaleToNextInplace(tSum1);
                    evaluator.RescaleToNextInplace(tSum2);
                    evaluator.RescaleToNextInplace(tSum3);
                    evaluator.RescaleToNextInplace(tSum4);
                }


                PrintScale(tSum1, "tSum1"); //Level 2
                PrintScale(tSum2, "tSum2"); //Level 2
                PrintScale(tSum3, "tSum3"); //Level 2
                PrintScale(tSum4, "tSum4"); //Level 2

                var ciphertexts1 = new List<Ciphertext>();
                ciphertexts1.Add(tSum1);
                ciphertexts1.Add(tSum2);
                ciphertexts1.Add(tSum3);
                ciphertexts1.Add(tSum4);

                //=================================================================
                evaluator.AddMany(ciphertexts1, kernel0); //Level 2
                //=================================================================
                PrintScale(kernel0, "kernel0"); //Level 2

                PrintCyprherText(decryptor, kernel0, encoder,"kernel0");

                Ciphertext kernel1 = new Ciphertext();
                //Level 1-> 2
                //=================================================================
                evaluator.MultiplyPlain(f0Encrypted, v10Plaintext1, tSum1);
                evaluator.MultiplyPlain(f1Encrypted, v11Plaintext1, tSum2);
                evaluator.MultiplyPlain(f2Encrypted, v12Plaintext1, tSum3);
                evaluator.MultiplyPlain(f3Encrypted, v13Plaintext1, tSum4);
                //=================================================================
                if (useRelinearizeInplace)
                {
                    Console.WriteLine("RelinearizeInplace sums 2");
                    evaluator.RelinearizeInplace(tSum1, relinKeys);
                    evaluator.RelinearizeInplace(tSum2, relinKeys);
                    evaluator.RelinearizeInplace(tSum3, relinKeys);
                    evaluator.RelinearizeInplace(tSum4, relinKeys);
                }

                if (useReScale)
                {
                    Console.WriteLine("useReScale sums 2");
                    evaluator.RescaleToNextInplace(tSum1);
                    evaluator.RescaleToNextInplace(tSum2);
                    evaluator.RescaleToNextInplace(tSum3);
                    evaluator.RescaleToNextInplace(tSum4);
                }

                ciphertexts1.Add(tSum1);
                ciphertexts1.Add(tSum2);
                ciphertexts1.Add(tSum3);
                ciphertexts1.Add(tSum4);



                Console.WriteLine("Second time : ");
                PrintScale(tSum1, "tSum1"); //Level 2
                PrintScale(tSum2, "tSum2"); //Level 2
                PrintScale(tSum3, "tSum3"); //Level 2
                PrintScale(tSum4, "tSum4"); //Level 2

                var ciphertexts2 = new List<Ciphertext>();
                ciphertexts2.Add(tSum1);
                ciphertexts2.Add(tSum2);
                ciphertexts2.Add(tSum3);
                ciphertexts2.Add(tSum4);
                
                
                //=================================================================
                evaluator.AddMany(ciphertexts2, kernel1); // Level 2
                //=================================================================
                PrintScale(kernel1, "kernel1");
                PrintCyprherText(decryptor, kernel1, encoder, "kernel1");

                Ciphertext kernel2 = new Ciphertext();

                //Level 1->2
                //=================================================================
                evaluator.MultiplyPlain(f0Encrypted, v20Plaintext1, tSum1);
                evaluator.MultiplyPlain(f1Encrypted, v21Plaintext1, tSum2);
                evaluator.MultiplyPlain(f2Encrypted, v22Plaintext1, tSum3);
                evaluator.MultiplyPlain(f3Encrypted, v23Plaintext1, tSum4);
                //=================================================================

                if (useRelinearizeInplace)
                {
                    Console.WriteLine("RelinearizeInplace sums 3");
                    evaluator.RelinearizeInplace(tSum1, relinKeys);
                    evaluator.RelinearizeInplace(tSum2, relinKeys);
                    evaluator.RelinearizeInplace(tSum3, relinKeys);
                    evaluator.RelinearizeInplace(tSum4, relinKeys);
                }



                if (useReScale)
                {
                    Console.WriteLine("useReScale sums 3");
                    evaluator.RescaleToNextInplace(tSum1);
                    evaluator.RescaleToNextInplace(tSum2);
                    evaluator.RescaleToNextInplace(tSum3);
                    evaluator.RescaleToNextInplace(tSum4);
                }

                var ciphertexts3 = new List<Ciphertext>();
                ciphertexts3.Add(tSum1);
                ciphertexts3.Add(tSum2);
                ciphertexts3.Add(tSum3);
                ciphertexts3.Add(tSum4);

                Console.WriteLine("Third time : ");
                PrintScale(tSum1, "tSum1"); //Level 2
                PrintScale(tSum2, "tSum2"); //Level 2
                PrintScale(tSum3, "tSum3"); //Level 2
                PrintScale(tSum4, "tSum4"); //Level 2

                //=================================================================
                evaluator.AddMany(ciphertexts3, kernel2);
                //=================================================================
                PrintScale(kernel2, "kernel2"); //Level 2
                
                PrintCyprherText(decryptor, kernel2, encoder, "kernel2");

                Ciphertext decision1 = new Ciphertext();
                Ciphertext decision2 = new Ciphertext();
                Ciphertext decision3 = new Ciphertext();

                PrintScale(decision1, "decision1"); //Level 0
                PrintScale(decision2, "decision2"); //Level 0
                PrintScale(decision3, "decision3"); //Level 0


                Ciphertext nKernel0 = new Ciphertext();
                Ciphertext nKernel1 = new Ciphertext();
                Ciphertext nKernel2 = new Ciphertext();

                //=================================================================
                evaluator.Negate(kernel0, nKernel0); 
                evaluator.Negate(kernel1, nKernel1); //Level 2
                evaluator.Negate(kernel2, nKernel2); //Level 2
                //=================================================================



                //nKernel0.Scale = scale; 
                //nKernel1.Scale = scale;
                //nKernel2.Scale = scale;
                double scale2 = Math.Pow(2.0, power);
                if (useReScale)
                {
                    scale2 = nKernel0.Scale;
                }

                encoder.Encode(coefficients[0][0], scale2, coef00PlainText);
                encoder.Encode(coefficients[0][1], scale2, coef01PlainText);
                encoder.Encode(coefficients[0][2], scale2, coef02PlainText);

                PrintScale(coef00PlainText, "coef00PlainText");
                PrintScale(coef01PlainText, "coef01PlainText");
                PrintScale(coef02PlainText, "coef02PlainText");



                if (useReScale)
                {
                    ParmsId lastParmsId = nKernel0.ParmsId;
                    evaluator.ModSwitchToInplace(coef00PlainText, lastParmsId);


                    lastParmsId = nKernel1.ParmsId;
                    evaluator.ModSwitchToInplace(coef01PlainText, lastParmsId);

                    lastParmsId = nKernel2.ParmsId;
                    evaluator.ModSwitchToInplace(coef02PlainText, lastParmsId);
                }

                PrintScale(nKernel0,  "nKernel0");   //Level 2
                PrintScale(nKernel1, "nKernel1");   //Level 2
                PrintScale(nKernel2,  "nKernel2");   //Level 2

                //Level 2->3
                //=================================================================
                evaluator.MultiplyPlain(nKernel0, coef00PlainText, decision1);
                evaluator.MultiplyPlain(nKernel1, coef01PlainText, decision2);
                evaluator.MultiplyPlain(nKernel2, coef02PlainText, decision3);
                //=================================================================





                if (useRelinearizeInplace)
                {
                    Console.WriteLine("RelinearizeInplace decisions");

                    evaluator.RelinearizeInplace(decision1, relinKeys);
                    evaluator.RelinearizeInplace(decision2, relinKeys);
                    evaluator.RelinearizeInplace(decision3, relinKeys);
                }


                if (useReScale)
                {
                    Console.WriteLine("Rescale decisions");

                    evaluator.RescaleToNextInplace(decision1);
                    evaluator.RescaleToNextInplace(decision2);
                    evaluator.RescaleToNextInplace(decision3);
                }


                PrintScale(decision1, "decision1"); //Level 3
                PrintScale(decision2, "decision2"); //Level 3
                PrintScale(decision3, "decision3"); //Level 3
                PrintCyprherText(decryptor, decision1, encoder, "decision1");
                PrintCyprherText(decryptor, decision2, encoder, "decision2");
                PrintCyprherText(decryptor, decision3, encoder, "decision3");

                //=================================================================
                //evaluator.RelinearizeInplace(decision1,keygen.RelinKeys());
                //evaluator.RelinearizeInplace(decision2, keygen.RelinKeys());
                //evaluator.RelinearizeInplace(decision3, keygen.RelinKeys());
                //=================================================================


                //PrintScale(decision1, "decision1");

                var decisions = new List<Ciphertext>();
                decisions.Add(decision1);
                decisions.Add(decision2);
                decisions.Add(decision3);

                Ciphertext decisionTotal = new Ciphertext();

                //=================================================================
                evaluator.AddMany(decisions, decisionTotal);
                //=================================================================
                PrintScale(decisionTotal, "decisionTotal"); 
                PrintCyprherText(decryptor, decisionTotal, encoder, "decision total");


                Ciphertext finalTotal = new Ciphertext();

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

                //=================================================================
                evaluator.AddPlainInplace(decisionTotal, interceptsPlainText);
                //=================================================================

                PrintScale(decisionTotal, "decisionTotal");  //Level 3
                List<double> result = PrintCyprherText(decryptor, decisionTotal, encoder, "finalTotal");
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(
                        $@"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\IrisSecureSVC_total_{power}_{useRelinearizeInplace}_{useReScale}.txt", !firstTime)
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
            int scale = 120;
            bool useRelinearizeInplace = true;
            bool useReScale = true;

            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(
                    $@"D:\GAL\Workspace\SecureCloudComputing\SEAL_test\SecureCloudComputing\IrisSVNSecured\Output\IrisSecureSVC_classification_result_{scale}_{useRelinearizeInplace}_{useReScale}.txt")
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

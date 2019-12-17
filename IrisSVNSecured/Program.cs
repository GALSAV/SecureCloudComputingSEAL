using Microsoft.Research.SEAL;
using System;
using System.Collections.Generic;

namespace IrisSVNSecured
{
    class Program
    {



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
                            }

                            kernels[i] = kernel;
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
                    }
                    else
                    {
                        starts[0] = 0;
                    }
                }

                int[] ends = new int[this.nRows];
                for (int i = 0; i < this.nRows; i++)
                {
                    ends[i] = this.weights[i] + starts[i];
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
                    }

                    for (int k = starts[0]; k < ends[0]; k++)
                    {
                        decision += kernels[k] * this.coefficients[0][k];
                    }

                    decision += this.intercepts[0];

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
            private double coef0;
            private double degree;

            public IrisSVC( int nRows, double[][] vectors, double[][] coefficients, double[] intercepts, double gamma, double coef0, double degree)
            {
                //this._nClasses = nClasses;
                //this.classes = new int[nClasses];
                //for (int i = 0; i < nClasses; i++)
                //{
                //    this.classes[i] = i;
                //}

                this.nRows = nRows;

                this.vectors = vectors;
                this.coefficients = coefficients;
                this.intercepts = intercepts;

                this.gamma = gamma;
                this.coef0 = coef0;
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
                        Console.WriteLine($" i = {i} , j = {j}");
                    }
                    Console.WriteLine("-------------------------------------------------------------------------");
                    kernels[i] = Math.Pow((this.gamma * kernel) + this.coef0, this.degree);
                }

                double decision = 0;
                for (int i = 0; i < kernels.Length; i++)
                {
                    decision += -kernels[i] * this.coefficients[0][i];
                }

                decision += this.intercepts[0];

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

            public IrisSecureSVC(int nRows, double[][] vectors, double[][] coefficients, double[] intercepts,
                int[] weights, String kernel, double gamma, double coef0, double degree)
            {
                //this._nClasses = nClasses;
                //this.classes = new int[nClasses];
                //for (int i = 0; i < nClasses; i++)
                //{
                //    this.classes[i] = i;
                //}

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

                EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);
                ulong polyModulusDegree = 8192;
                parms.PolyModulusDegree = polyModulusDegree;
                parms.CoeffModulus = CoeffModulus.Create(
                    polyModulusDegree, new int[] { 60, 40, 40, 60 });


                double scale = Math.Pow(2.0, 40);

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

                Plaintext fPlaintext1 = new Plaintext();
                Plaintext fPlaintext2 =  new Plaintext();
                Plaintext fPlaintext3 = new Plaintext();
                Plaintext fPlaintext4 = new Plaintext();


                encoder.Encode(features[0], scale, fPlaintext1);
                encoder.Encode(features[1], scale, fPlaintext2);
                encoder.Encode(features[2], scale, fPlaintext3);
                encoder.Encode(features[3], scale, fPlaintext4);

                Ciphertext f1Encrypted = new Ciphertext();
                Ciphertext f2Encrypted = new Ciphertext();
                Ciphertext f3Encrypted = new Ciphertext();
                Ciphertext f4Encrypted = new Ciphertext();
                encryptor.Encrypt(fPlaintext1, f1Encrypted);
                encryptor.Encrypt(fPlaintext2, f2Encrypted);
                encryptor.Encrypt(fPlaintext3, f3Encrypted);
                encryptor.Encrypt(fPlaintext4, f4Encrypted);




                Plaintext v00Plaintext1 = new Plaintext();
                Plaintext v01Plaintext1 = new Plaintext();
                Plaintext v02Plaintext1 = new Plaintext();
                Plaintext v03Plaintext1 = new Plaintext();
                Plaintext v10Plaintext1 = new Plaintext();
                Plaintext v11Plaintext1 = new Plaintext();
                Plaintext v12Plaintext1 = new Plaintext();
                Plaintext v13Plaintext1 = new Plaintext();


                encoder.Encode(vectors[0][0], scale, v00Plaintext1);
                encoder.Encode(vectors[0][1], scale, v01Plaintext1);
                encoder.Encode(vectors[0][2], scale, v02Plaintext1);
                encoder.Encode(vectors[0][3], scale, v03Plaintext1);
                encoder.Encode(vectors[1][0], scale, v10Plaintext1);
                encoder.Encode(vectors[1][1], scale, v11Plaintext1);
                encoder.Encode(vectors[1][2], scale, v12Plaintext1);
                encoder.Encode(vectors[1][3], scale, v13Plaintext1);


                Ciphertext kernel0 = new Ciphertext();
                Ciphertext tSum1 = new Ciphertext();
                Ciphertext tSum2 = new Ciphertext();
                Ciphertext tSum3 = new Ciphertext();
                Ciphertext tSum4 = new Ciphertext();
                Ciphertext tSumTotal = new Ciphertext();
                evaluator.MultiplyPlain(f1Encrypted, v00Plaintext1, tSum1);
                evaluator.MultiplyPlain(f1Encrypted, v01Plaintext1, tSum2);
                evaluator.MultiplyPlain(f1Encrypted, v02Plaintext1, tSum3);
                evaluator.MultiplyPlain(f1Encrypted, v02Plaintext1, tSum4);
                var ciphertexts = new List<Ciphertext>();
                ciphertexts.Add(tSum1);
                ciphertexts.Add(tSum2);
                ciphertexts.Add(tSum3);
                ciphertexts.Add(tSum4);
                evaluator.AddMany(ciphertexts, tSumTotal);


                //evaluator.AddPlain();

                double[] kernels = new double[vectors.Length];
                double kernel;
                switch (this.kernel)
                {
                    case Kernel.Linear:
                        // <x,x'>
                        for (int i = 0; i < 4; i++)
                        {
                            kernel = 0;
                            for (int j = 0; j < 4; j++)
                            {
                                kernel += this.vectors[i][j] * features[j];
                            }

                            kernels[i] = kernel;
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

                //int[] starts = new int[this.nRows];
                //for (int i = 0; i < this.nRows; i++)
                //{
                //    if (i != 0)
                //    {
                //        int start = 0;
                //        for (int j = 0; j < i; j++)
                //        {
                //            start += this.weights[j];
                //        }

                //        starts[i] = start;
                //    }
                //    else
                //    {
                //        starts[0] = 0;
                //    }
                //}

                //int[] ends = new int[this.nRows];
                //for (int i = 0; i < this.nRows; i++)
                //{
                //    ends[i] = this.weights[i] + starts[i];
                //}

                //if (this._nClasses == 2)
                //{
                double decision = 0;
                for (int i = 0; i < kernels.Length; i++)
                {
                    decision += -kernels[i] * this.coefficients[0][i];
                }

                //double decision = 0;
                //Console.WriteLine($"start[1] = {starts[1]} , ends[1] = {ends[1]}");
                //for (int k = starts[1]; k < ends[1]; k++)
                //{
                //    decision += kernels[k] * this.coefficients[0][k];
                //    Console.WriteLine($"decision = {decision}   k={k}");
                //}

                //Console.WriteLine();
                //Console.WriteLine($"start[0] = {starts[0]} , ends[0] = {ends[0]}");
                //for (int k = starts[0]; k < ends[0]; k++)
                //{
                //    decision += kernels[k] * this.coefficients[0][k];
                //    Console.WriteLine($"decision = {decision} k={k}");
                //}

                decision += this.intercepts[0];

                if (decision > 0)
                {
                    return 0;
                }

                return 1;

            }

        }




        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            if (args.Length == 4)
            {

                // Features:
                double[] features = new double[args.Length];
                for (int i = 0, l = args.Length; i < l; i++)
                {
                    features[i] = Double.Parse(args[i]);
                }

               


                double[][] vectors = new double[2][];

                vectors[0] = new[] { 5.1, 3.3, 1.7, 0.5 };
                vectors[1] = new[] { 5.1, 2.5, 3.0, 1.1 };

                double[][] coefficients = new double[1][];
                coefficients[0] = new double[] { -0.009372190880663269, 0.009372190880663269 } ;
                double[] intercepts = { 0.9087355561683588 };
                //int[] weights = {1, 1};

                IrisSVC clf = new IrisSVC( 2, vectors, coefficients, intercepts/*, weights, "poly"*/, 0.25, 0.0, 3);
                int estimation = clf.Predict(features);
                Console.WriteLine($"estimation is : {estimation} ");
                Console.WriteLine("End");
            }


        }
    }
}

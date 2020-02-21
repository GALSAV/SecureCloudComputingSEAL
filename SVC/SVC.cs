using System;
using System.Collections.Generic;

namespace SVC
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

                        var d = (this.gamma * kernel);

                        kernels[i] = Math.Pow(d + this.coef0, this.degree);
                        Console.WriteLine($"kernels[{i}] =  {kernels[i]} kernel = {kernel} d= {d}");
                        Console.WriteLine("-----------------------------------------------------");
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
                    Console.WriteLine($"kernels[{i}] = " + kernels[i]);
                    Console.WriteLine("-----------------------------------------------------");
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
}

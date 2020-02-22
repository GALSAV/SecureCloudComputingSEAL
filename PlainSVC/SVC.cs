using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;


namespace PlainSVC
{
    public class SVC
    {

        public const bool USE_OUTPUT = false;

        private enum Kernel
        {
            Linear,
            Poly,
            Rbf,
            Sigmoid
        }

        private readonly int _nClasses;
        private readonly int _nRows;
        private readonly int[] _classes;
        private readonly double[][] _vectors;
        private readonly double[][] _coefficients;
        private readonly double[] _intercepts;
        private readonly int[] _weights;
        private readonly Kernel _kernel;
        private readonly double _gamma;
        private readonly double _coef0;
        private readonly double _degree;

        public SVC(int nClasses, int nRows, double[][] vectors, double[][] coefficients, double[] intercepts, int[] weights, String kernel, double gamma, double coef0, double degree)
        {


           PrintConsole($"Create SVC with nClasses = {nClasses} , ");
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

            this._kernel = (Kernel)System.Enum.Parse(typeof(Kernel), kernel);
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
                           
                        }
                        //PrintConsole($"kernel += this.vectors[{i}][{j}] * features[{j}]");

                        kernels[i] = kernel;
                        PrintConsole($"inner product TotalValue {i} : {kernel}");
                        PrintConsole("-----------------------------------------------------");
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
                       PrintConsole($"inner product TotalValue {i} : {kernel}");
                        kernels[i] = Math.Pow((this._gamma * kernel) + this._coef0, this._degree);
                       PrintConsole($"kernels[{i}] = {kernels[i]}");
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

           PrintConsole("Calculate weights : ");
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
                   PrintConsole($"starts[{i}] = {start}");

                }
                else
                {
                    starts[0] = 0;
                   PrintConsole($"starts[0] = 0");
                }
            }

            int[] ends = new int[this._nRows];
            for (int i = 0; i < this._nRows; i++)
            {
                ends[i] = this._weights[i] + starts[i];
               PrintConsole($"ends[{i}] = this.weights[{i}] + starts[{i}]");
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
                   PrintConsole($"starts1 : decision += kernels[{k}] * this.coefficients[0][{k}]");
                   PrintConsole($"starts1 : decision = {decision}");
                }

                for (int k = starts[0]; k < ends[0]; k++)
                {
                    decision += kernels[k] * this._coefficients[0][k];
                   PrintConsole($"starts0 : decision += kernels[{k}] * this.coefficients[0][{k}]");
                   PrintConsole($"starts0 : decision = {decision}");
                }

               PrintConsole($"Total decision = {decision}");
                decision += this._intercepts[0];

               PrintConsole($"decision = {decision}");
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


        public void PrintConsole(String line)
        {
            if (!USE_OUTPUT)
                return;
            Console.WriteLine (line);
        }
    }
}

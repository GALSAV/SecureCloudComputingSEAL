using Microsoft.VisualBasic.FileIO;
using PlainSVC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;


namespace IrisPlain
{
    class IrisLinearPlain
    {
		/*
		 *  Class for a Plain client of Iris classification
		 *  The application loads samples from Iris dataset which is stored in the resource of the application
		 *  or as program arguments.
		 *  A folder c:\Output must exist in order to properly run this application
		 */

		//Directory for storing the output file, 
		// the output file is in format:
		// #sample,classification class (0,1), svm total result
        private const string OutputDir = @"C:\Output\";

        static void Main(string[] args)
        {
            Console.WriteLine("Plain Iris");
            double[][] features;
            int numOfRows = 0;
			// Option to load from args and not the whole dataset that is stored in resources
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
            else // load the whole dataset from resources
            {
                List<double[]> rows = new List<double[]>();
               
                var bytes = Properties.Resources.iris;
                numOfRows = 0;
                features = SVCUtilities.SvcUtilities.LoadFeatures(bytes, 4, ref numOfRows);

            }

			// The support vectors and the coefficients of the Iris classification algorithm
            double[][] vectors = new double[3][];

            vectors[0] = new[] { 4.5, 2.3, 1.3, 0.3 };
            vectors[1] = new[] { 5.1, 3.3, 1.7, 0.5 };
            vectors[2] = new[] { 5.1, 2.5, 3.0, 1.1 };

            double[][] coefficients = new double[1][];
            coefficients[0] = new double[] { -0.07724840262003278, -0.6705185831514366, 0.7477669857714694 };
            double[] intercepts = { 1.453766563649063 };
            int[] weights = { 2, 1 };

            Console.WriteLine("SVC : ");
			// Estimator constructor
            Svc clf = new Svc(2, 2, vectors, coefficients, intercepts, weights, "Linear", 0.25, 0.0, 3);
            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(
                   $@"{OutputDir}IrisLinearPlain_{DateTime.Now.Day}_{DateTime.Now.ToShortTimeString().ToString().Replace(":","_")}.txt")
            )
            {
                Stopwatch totalTime = new Stopwatch();
                totalTime.Start();
				//Run prediction onthe loaded samples
                for (int i = 0; i < numOfRows; i++)
                {
                    double finalResult=-10000;
                    int estimation = clf.Predict(features[i],out finalResult);
                    Console.WriteLine($"\n ************************************************");
                    Console.WriteLine($"SVC estimation{i} is : {estimation} , result : {finalResult}");
					//write classification result to file
                    file.WriteLine($"{i} , {estimation} , {finalResult} ");
                    Console.WriteLine($"************************************************ \n");
                }
                totalTime.Stop();
				//Write performance data to file
                file.WriteLine($" Total time for {numOfRows} samples :  {totalTime.ElapsedMilliseconds} ms  ");
                file.WriteLine($" Avg time  :  {totalTime.ElapsedMilliseconds *1000 / numOfRows} microSec ");
            }
        }
    }
}

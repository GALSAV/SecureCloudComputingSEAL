using Microsoft.VisualBasic.FileIO;
using PlainSVC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;


namespace IrisPlain
{
    class IrisPlain
    {

        private const string OutputDir = @"C:\Output\";

        static void Main(string[] args)
        {
            Console.WriteLine("Plain Iris");
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
                    features[i / 4][i % 4] = Double.Parse(args[i]);
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

                    features[i] = rows[i]; //new double[numOfColums];
                }
            }


            double[][] vectors = new double[3][];

            vectors[0] = new[] { 5.1, 3.3, 1.7, 0.5 };
            vectors[1] = new[] { 4.8, 3.4, 1.9, 0.2 };
            vectors[2] = new[] { 5.1, 2.5, 3.0, 1.1 };

            double[][] coefficients = new double[1][];
            coefficients[0] = new double[] { -0.7407784813992192, -0.0025023664254470897, 0.7432808478246663 };
            double[] intercepts = { 0.9055182807973224 };
            int[] weights = { 2, 1 };

            Console.WriteLine("SVC : ");
            SVC clf = new SVC(2, 2, vectors, coefficients, intercepts, weights, "Linear", 0.25, 0.0, 3);
            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(
                   $@"{OutputDir}IrisPlain_{DateTime.Now.Day}_{DateTime.Now.ToShortTimeString().ToString().Replace(":","_")}.txt")
            )
            {
                Stopwatch totalTime = new Stopwatch();
                totalTime.Start();
                for (int i = 0; i < numOfRows; i++)
                {
                    double finalResult=-10000;
                    int estimation = clf.Predict(features[i],out finalResult);
                    Console.WriteLine($"\n ************************************************");
                    Console.WriteLine($"SVC estimation{i} is : {estimation} , result : {finalResult}");
                    file.WriteLine($"{i} , {estimation} , {finalResult} ");
                    Console.WriteLine($"************************************************ \n");
                }
                totalTime.Stop();
                file.WriteLine($" Total time for {numOfRows} samples :  {totalTime.ElapsedMilliseconds} ms  ");
                file.WriteLine($" Avg time  :  {totalTime.ElapsedMilliseconds *1000 / numOfRows} microSec ");
            }
        }
    }
}

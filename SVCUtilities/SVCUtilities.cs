using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVCUtilities
{
    public static class SvcUtilities
    {
        private const bool PRINT_SCALE = false;
        private const bool PrintExactScale = false;
        private const bool PrintCipherText = false;
        /*
		 * Utility class for common static functions used in the solution
		 */

        //Function for parsing CSV dataset into multidimensional array
        public static double[][] LoadFeatures(byte[] bytes, int numberOfFeatuters, ref int numOfRows)
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

		//Function to convert from two dimensional array to Jagged Array.
        public static T[][] ToJaggedArray<T>(this T[,] twoDimensionalArray)
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


        //Function for conditionally printing the scale of the ciphertext for debug 
        public static void PrintScale(Ciphertext ciphertext, String name)
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
        public static void PrintScale(Plaintext plaintext, String name)
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
        public static List<double> PrintCyprherText(Decryptor decryptor, Ciphertext ciphertext, CKKSEncoder encoder, String name, bool print = false)
        {
            
            if (decryptor == null) return null;
            Plaintext plainResult = new Plaintext();
            decryptor.Decrypt(ciphertext, plainResult);
            List<double> result = new List<double>();
            encoder.Decode(plainResult, result);
            if (!PrintCipherText && !print) return result;
            Console.WriteLine($"{name} TotalValue = {result[0]}");
            return result;
        }
    }


}

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
    }


}

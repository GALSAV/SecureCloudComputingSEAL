using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using SecureSVC;
using System;
using System.Collections.Generic;
using System.IO;

namespace IrisSecured
{
    class IrisSecured
    {
        private const string OutputDir = @"C:\Output\";

        static void Main(string[] args)
        {
            Console.WriteLine("Secure Iris");
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

            Console.WriteLine("SecureSVC : ");

            EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);

            ulong polyModulusDegree = 16384;
            int power = 40;

            if (power >= 20 && power < 40)
            {
                parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
                    new int[] { 60, 20, 21, 22, 23, 24, 25, 26, 27, 60 });
            }
            else if (power >= 40 && power < 60)
            {
                parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
                    new int[] { 60, 40, 40, 40, 40, 40, 40, 40, 60 });
            }
            else if (power == 60)
            {
                polyModulusDegree = 32768;
                parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree,
                    new int[] { 60, 60, 60, 60, 60, 60, 60, 60, 60 });
            }
            parms.PolyModulusDegree = polyModulusDegree;

            

            var _context = new SEALContext(parms);
            KeyGenerator keygen = new KeyGenerator(_context);
            var publicKey = keygen.PublicKey;
            var secretKey = keygen.SecretKey;
            var relinKeys = keygen.RelinKeys();

            var galoisKeys = keygen.GaloisKeys();
            var _encryptor = new Encryptor(_context, publicKey);
           
           var  _decryptor = new Decryptor(_context, secretKey);
           var  _encoder = new CKKSEncoder(_context);

            SVC clf = new SVC(vectors, coefficients, intercepts, "Linear", 0.25, 0.0, 3,40,publicKey,secretKey,relinKeys,galoisKeys,1,4);
            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(
                   $@"{OutputDir}IrisSecured_{DateTime.Now.Day}_{DateTime.Now.ToShortTimeString().ToString().Replace(":", "_")}.txt")
            )
            {
                for (int i = 0; i < numOfRows; i++)
                {
                    double finalResult = -10000;
                    var plaintexts = new Plaintext();
                    var featuresCiphertexts = new Ciphertext();
                    _encoder.Encode(features[i], power, plaintexts);
                    _encryptor.Encrypt(plaintexts, featuresCiphertexts);
                    var cyphetResult= clf.Predict(featuresCiphertexts, true,true);
                    Plaintext plainResult = new Plaintext();
                    _decryptor.Decrypt(cyphetResult, plainResult);
                    List<double> result = new List<double>();
                    _encoder.Decode(plainResult, result);
                    finalResult = result[0];
                    int estimation = finalResult > 0 ? 1 : 0;
                    Console.WriteLine($"\n ************************************************");
                    Console.WriteLine($"SVC estimation{i} is : {estimation} , result : {finalResult}");
                    file.WriteLine($"{i} , {estimation} , {finalResult} ");
                    Console.WriteLine($"************************************************ \n");
                }
            }
        }
    }
}

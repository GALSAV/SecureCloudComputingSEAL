using Microsoft.Research.SEAL;
using System;
using System.Collections.Generic;
using System.Numerics;


namespace SimpleSealTest
{
    class Program
    {
        private static void Main(string[] args)
        {

            EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);

            //if (power < 60)
            //{
                ulong polyModulusDegree = 8192;
                parms.PolyModulusDegree = polyModulusDegree;
                parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree, new int[] { 60, 40, 40, 60 });
            //}
            //else
            //{
            //    ulong polyModulusDegree = 16384;
            //    parms.PolyModulusDegree = polyModulusDegree;
            //    parms.CoeffModulus = CoeffModulus.Create(polyModulusDegree, new int[] { 60, 60, 60, 60, 60, 60 });
            //}
            //

            double scale = Math.Pow(2.0, 30);

            SEALContext context = new SEALContext(parms);

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

            double[]  inputs = new double[] { 1.0, 1.1, 2.2, 3.3, 4.4, 5.5, 6.6, 7.7, 8.8, 9.9 };
            double[]  weights = new double[] { 2.0, -3.0, 4.0, -5.0, 2.0, -1.0, 2.0, -1.0, 2.0, -1.0 };
            Ciphertext c  = new Ciphertext();
            Plaintext inputsPlaintext = new Plaintext();
            Plaintext weightsPlaintext = new Plaintext();

            encoder.Encode(inputs, scale, inputsPlaintext);
            encoder.Encode(weights, scale, weightsPlaintext);

            encryptor.Encrypt(inputsPlaintext,c);


            //evaluator.MultiplyPlainInplace(c,weightsPlaintext);

            Plaintext outputsPlaintext = new Plaintext();
            decryptor.Decrypt(c, outputsPlaintext);


            List<double> result = new List<double>();
            encoder.Decode(outputsPlaintext, result);

            Utilities.PrintVector(result,20);



            //for (int i = 0; i < weights.Length; i++)
            //{
            //    p[i] = new Plaintext();

            //    encryptor.Encrypt(weights[i],c[i]);
            //}
            //for

            //            vector<double> weights{ 2.0, -1.0, 2.0, -1.0, 2.0, -1.0, 2.0, -1.0, 2.0, -1.0 };

            //Plaintext p = new Plaintext();
            //encoder.Encode(inputs, scale, p);









        }
    }
}

using Microsoft.Research.SEAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace SimpleSealTest
{
    class BFV
    {
        private  SEALContext context;
        private Encryptor encryptor;
        private Evaluator evaluator;
        private Decryptor decryptor;
        private KeyGenerator keygen;
        private const int ModulosValue = 1024;
        private const Plaintext plainOne = new Plaintext("1");

        public BFV()
        {
            context = createContext();
            keygen = new KeyGenerator(context);
            PublicKey publicKey = keygen.PublicKey;
            SecretKey secretKey = keygen.SecretKey;

            encryptor = new Encryptor(context, publicKey);

            //Server side
            evaluator = new Evaluator(context);

            decryptor = new Decryptor(context, secretKey);
        }

        public static void Main(string[] args)
        {
            for (ulong i = 0; i < 100; i++)
            {
                CalculateBFV(i);
            }
        }

        public  void CalculateBFV(ulong evalVal)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"~~~~~~ {evalVal} A naive way to calculate 4(x^2+1)(x+1)^2. ~~~~~~");

            /*
                Plaintexts in the BFV scheme are:
                    polynomials of degree less than the degree of the polynomial modulus
                    and coefficients integers modulo the plaintext modulus
            */

            Plaintext xPlain = new Plaintext(evalVal.ToString());
    
            Ciphertext xEncrypted = new Ciphertext();
            encryptor.Encrypt(xPlain, xEncrypted);

            var encryptedResult = ComputePolynomEvaluationNaive(xEncrypted);

            //decryptor.Decrypt(encryptedResult, decryptedResult);
   
            //expectedValue = (((evalVal * evalVal + 1) * (evalVal + 1) * (evalVal + 1)) * 4) % ModulosValue;
            //if (decryptedResult[0] == expectedValue)
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            //}
            //else
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            //}

            // /Compute the same polinom evaluation but with relinearization




            Console.WriteLine("~~~~~~ A better way to calculate 4(x^2+1)(x+1)^2. ~~~~~~");

            //Utilities.PrintLine();
            //Console.WriteLine("Generate relinearization keys.");
            /*
            We now repeat the computation relinearizing after each multiplication.
            */

            var xSqPlusOne = XSqPlusOneRelinearization(xEncrypted);


            //Console.WriteLine($"    + size of xSquared: {xSquared.Size}");
            //Console.WriteLine("    + size of xSquared (after relinearization): {0}", xSquared.Size);
            //Console.WriteLine("    + noise budget in xSqPlusOne: {0} bits", decryptor.InvariantNoiseBudget(xSqPlusOne));
            //Console.Write("    + decryption of xSqPlusOne: ");
//            decryptor.Decrypt(xSqPlusOne, decryptedResult);
//            expectedValue = (evalVal * evalVal + 1) % ModulosValue;
//            if (decryptedResult[0] == expectedValue)
//            {
//                Console.WriteLine($"0x{decryptedResult} ...... Correct.");
//            }
//            else
//            {
//                Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
//            }
            //Console.WriteLine($"0x{decryptedResult} ...... Correct.");

            //Utilities.PrintLine();
            Ciphertext xPlusOne = new Ciphertext();
            Console.WriteLine("Compute xPlusOne (x+1),");
            Console.WriteLine(new string(' ', 13) + "then compute and relinearize xPlusOneSq ((x+1)^2).");
            evaluator.AddPlain(xEncrypted, plainOne, xPlusOne);
            evaluator.Square(xPlusOne, xPlusOneSq);
            //Console.WriteLine($"    + size of xPlusOneSq: {xPlusOneSq.Size}");
            evaluator.RelinearizeInplace(xPlusOneSq, relinKeys);
            //Console.WriteLine("    + noise budget in xPlusOneSq: {0} bits", decryptor.InvariantNoiseBudget(xPlusOneSq));
            //Console.Write("    + decryption of xPlusOneSq: ");
            decryptor.Decrypt(xPlusOneSq, decryptedResult);
            expectedValue = ((evalVal + 1) * (evalVal + 1)) % ModulosValue;
            if (decryptedResult[0] == expectedValue)
            {
                Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            }
            else
            {
                Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            }

            //Console.WriteLine($"0x{decryptedResult} ...... Correct.");

            //Utilities.PrintLine();
            Console.WriteLine("Compute and relinearize encryptedResult (4(x^2+1)(x+1)^2).");
            evaluator.MultiplyPlainInplace(xSqPlusOne, plainFour);
            evaluator.Multiply(xSqPlusOne, xPlusOneSq, encryptedResult);
            //Console.WriteLine($"    + size of encryptedResult: {encryptedResult.Size}");
            evaluator.RelinearizeInplace(encryptedResult, relinKeys);
            //Console.WriteLine("    + size of encryptedResult (after relinearization): {0}",
            //    encryptedResult.Size);
            //Console.WriteLine("    + noise budget in encryptedResult: {0} bits",
            //    decryptor.InvariantNoiseBudget(encryptedResult));

            //Console.WriteLine();
            //Console.WriteLine("NOTE: Notice the increase in remaining noise budget.");

            /*
            Relinearization clearly improved our noise consumption. We have still plenty
            of noise budget left, so we can expect the correct answer when decrypting.
            */
            //Utilities.PrintLine();
            //Console.WriteLine("Decrypt encrypted_result (4(x^2+1)(x+1)^2).");
            decryptor.Decrypt(encryptedResult, decryptedResult);
            //Console.WriteLine("    + decryption of 4(x^2+1)(x+1)^2 = 0x{0} ...... Correct.", decryptedResult);
            expectedValue = (((evalVal * evalVal + 1) * (evalVal + 1) * (evalVal + 1)) * 4) % ModulosValue;
            if (decryptedResult[0] == expectedValue)
            {
                Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            }
            else
            {
                Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            }
        }

        private Ciphertext XSqPlusOneRelinearization(Ciphertext xEncrypted)
        {
            RelinKeys relinKeys = keygen.RelinKeys();


            Ciphertext xSquared = new Ciphertext();
            Ciphertext xSqPlusOne = new Ciphertext();


            evaluator.Square(xEncrypted, xSquared);
            evaluator.RelinearizeInplace(xSquared, relinKeys);
            evaluator.AddPlain(xSquared, plainOne, xSqPlusOne);
            return xSqPlusOne;
        }

        private Ciphertext ComputePolynomEvaluationNaive(Ciphertext xEncrypted)
        {
/*************************************
            Compute xSqPlusOne (x^2+1).
            ****************************************/
            var xSqPlusOne = XSqPlusOne(xEncrypted);


            //Plaintext decryptedResult = new Plaintext();
            //decryptor.Decrypt(xSqPlusOne, decryptedResult);

            //ulong expectedValue = (evalVal * evalVal + 1) % ModulosValue;
            //if (decryptedResult[0] == expectedValue)
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            //}
            //else
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            //}


            /*
            Next, we compute (x + 1)^2.
            */

            var xPlusOneSq = XPlusOneSq(xEncrypted);


            //decryptor.Decrypt(xPlusOneSq, decryptedResult);
            //expectedValue = ((evalVal + 1) * (evalVal + 1)) % ModulosValue;
            //if (decryptedResult[0] == expectedValue)
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            //}
            //else
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            //}


            /*
            Finally, we multiply (x^2 + 1) * (x + 1)^2 * 4.
            */
            //Utilities.PrintLine();


            var encryptedResult = ComputeEncryptedPolynomResult(xSqPlusOne, xPlusOneSq);
            return encryptedResult;
        }

        private Ciphertext ComputeEncryptedPolynomResult(Ciphertext xSqPlusOne, Ciphertext xPlusOneSq)
        {
            Console.WriteLine("Compute encryptedResult (4(x^2+1)(x+1)^2).");
            Ciphertext encryptedResult = new Ciphertext();
            Plaintext plainFour = new Plaintext("4");
            evaluator.MultiplyPlainInplace(xSqPlusOne, plainFour);
            evaluator.Multiply(xSqPlusOne, xPlusOneSq, encryptedResult);
            return encryptedResult;
        }

        private Ciphertext XPlusOneSq(Ciphertext xEncrypted)
        {
            Console.WriteLine("Compute xPlusOneSq ((x+1)^2).");
            Ciphertext xPlusOneSq = new Ciphertext();
            evaluator.AddPlain(xEncrypted, plainOne, xPlusOneSq);
            evaluator.SquareInplace(xPlusOneSq);
            return xPlusOneSq;
        }

        private Ciphertext XSqPlusOne(Ciphertext xEncrypted)
        {
            Console.WriteLine("Compute xSqPlusOne (x^2+1).");
            Ciphertext xSqPlusOne = new Ciphertext();
            evaluator.Square(xEncrypted, xSqPlusOne);

           
            evaluator.AddPlainInplace(xSqPlusOne, plainOne);
            return xSqPlusOne;
        }

        private static SEALContext createContext()
        {
            EncryptionParameters parms = new EncryptionParameters(SchemeType.BFV);
            ulong polyModulusDegree = 4096;
            parms.PolyModulusDegree = polyModulusDegree;
            parms.CoeffModulus = CoeffModulus.BFVDefault(polyModulusDegree);
            parms.PlainModulus = new SmallModulus(ModulosValue);
            SEALContext context = new SEALContext(parms);
            return context;
        }
    }
}

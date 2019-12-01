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
        private static readonly Plaintext plainOne = new Plaintext("1");
        private static readonly Plaintext plainFour = new Plaintext("4");

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
                BFV bfv = new BFV();
                bfv.CalculateBFV(i);
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

            Ciphertext encrypedEvaluationNaive = ComputePolynomEvaluationNaive(xEncrypted);

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
            var xPlusOneSq = XPlusOneSqRelinearize(xEncrypted);


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
            //Console.WriteLine("    + noise budget in xPlusOneSq: {0} bits", decryptor.InvariantNoiseBudget(xPlusOneSq));
            //Console.Write("    + decryption of xPlusOneSq: ");
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

            //Console.WriteLine($"0x{decryptedResult} ...... Correct.");

            //Utilities.PrintLine();
            var polinomEvaluation = PolinomEvaluationRelinearization(xSqPlusOne, xPlusOneSq);

            //decryptor.Decrypt(encryptedResult, decryptedResult);
            ////Console.WriteLine("    + decryption of 4(x^2+1)(x+1)^2 = 0x{0} ...... Correct.", decryptedResult);
            //expectedValue = (((evalVal * evalVal + 1) * (evalVal + 1) * (evalVal + 1)) * 4) % ModulosValue;
            //if (decryptedResult[0] == expectedValue)
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            //}
            //else
            //{
            //    Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            //}
        }

        private Ciphertext PolinomEvaluationRelinearization(Ciphertext xSqPlusOne, Ciphertext xPlusOneSq)
        {
            Console.WriteLine("Compute and relinearize encryptedResult (4(x^2+1)(x+1)^2).");
            evaluator.MultiplyPlainInplace(xSqPlusOne, plainFour);
            Ciphertext polinomEvaluation = new Ciphertext();
            evaluator.Multiply(xSqPlusOne, xPlusOneSq, polinomEvaluation);
            RelinKeys relinKeys = keygen.RelinKeys();
            evaluator.RelinearizeInplace(polinomEvaluation, relinKeys);
            return polinomEvaluation;
        }

        private Ciphertext XPlusOneSqRelinearize(Ciphertext xEncrypted)
        {
            Ciphertext xPlusOne = new Ciphertext();
            evaluator.AddPlain(xEncrypted, plainOne, xPlusOne);
            Ciphertext xPlusOneSq = new Ciphertext();
            ;
            evaluator.Square(xPlusOne, xPlusOneSq);
            RelinKeys relinKeys = keygen.RelinKeys();
            evaluator.RelinearizeInplace(xPlusOneSq, relinKeys);
            return xPlusOneSq;
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

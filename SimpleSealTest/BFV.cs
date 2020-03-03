using Microsoft.Research.SEAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace SimpleSealTest
{
    class Bfv
    {
        private  SEALContext _context;
        private Encryptor _encryptor;
        private Evaluator _evaluator;
        private Decryptor _decryptor;
        private KeyGenerator _keygen;
        private const int ModulosValue = 1024;
        private static readonly Plaintext PlainOne = new Plaintext("1");
        private static readonly Plaintext PlainFour = new Plaintext("4");

        public Bfv()
        {
            _context = CreateContext();
            _keygen = new KeyGenerator(_context);
            PublicKey publicKey = _keygen.PublicKey;
            SecretKey secretKey = _keygen.SecretKey;

            _encryptor = new Encryptor(_context, publicKey);

            //Server side
            _evaluator = new Evaluator(_context);

            _decryptor = new Decryptor(_context, secretKey);
        }

        public static void Main(string[] args)
        {
            for (ulong i = 0; i < 50; i++)
            {
                Bfv bfv = new Bfv();
                bfv.CalculateBfv(i);
            }
        }

        public  void CalculateBfv(ulong evalVal)
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
            var s = evalVal.ToString("X");
            Plaintext xPlain = new Plaintext(s);
    
            Ciphertext xEncrypted = new Ciphertext();
            _encryptor.Encrypt(xPlain, xEncrypted);

            Ciphertext encrypedEvaluationNaive = ComputePolynomEvaluationNaive(xEncrypted);
            Console.WriteLine($"     ---- {_decryptor.InvariantNoiseBudget(encrypedEvaluationNaive)}");
            Plaintext decryptedResult = new Plaintext();
            _decryptor.Decrypt(encrypedEvaluationNaive, decryptedResult);

            //var ulong2Hex = Utilities.Ulong2Hex(evalVal);
            var expectedValue = (((evalVal * evalVal + 1) * (evalVal + 1) * (evalVal + 1)) * 4) % ModulosValue;
            if (decryptedResult[0] == expectedValue)
            {
                Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            }
            else
            {
                Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            }

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
            decryptedResult = new Plaintext();
            _decryptor.Decrypt(polinomEvaluation, decryptedResult);
            if (decryptedResult[0] == expectedValue)
            {
                Console.WriteLine($"0x{decryptedResult} ...... Correct.");
            }
            else
            {
                Console.WriteLine($"0x{decryptedResult} ...... ERROR.");
            }

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
            _evaluator.MultiplyPlainInplace(xSqPlusOne, PlainFour);
            Ciphertext polinomEvaluation = new Ciphertext();
            _evaluator.Multiply(xSqPlusOne, xPlusOneSq, polinomEvaluation);
            RelinKeys relinKeys = _keygen.RelinKeys();
            _evaluator.RelinearizeInplace(polinomEvaluation, relinKeys);
            return polinomEvaluation;
        }

        private Ciphertext XPlusOneSqRelinearize(Ciphertext xEncrypted)
        {
            Ciphertext xPlusOne = new Ciphertext();
            _evaluator.AddPlain(xEncrypted, PlainOne, xPlusOne);
            Ciphertext xPlusOneSq = new Ciphertext();
            ;
            _evaluator.Square(xPlusOne, xPlusOneSq);
            RelinKeys relinKeys = _keygen.RelinKeys();
            _evaluator.RelinearizeInplace(xPlusOneSq, relinKeys);
            return xPlusOneSq;
        }

        private Ciphertext XSqPlusOneRelinearization(Ciphertext xEncrypted)
        {
            RelinKeys relinKeys = _keygen.RelinKeys();


            Ciphertext xSquared = new Ciphertext();
            Ciphertext xSqPlusOne = new Ciphertext();


            _evaluator.Square(xEncrypted, xSquared);
            _evaluator.RelinearizeInplace(xSquared, relinKeys);
            _evaluator.AddPlain(xSquared, PlainOne, xSqPlusOne);
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
           
            _evaluator.MultiplyPlainInplace(xSqPlusOne, PlainFour);
            _evaluator.Multiply(xSqPlusOne, xPlusOneSq, encryptedResult);
            return encryptedResult;
        }

        private Ciphertext XPlusOneSq(Ciphertext xEncrypted)
        {
            Console.WriteLine("Compute xPlusOneSq ((x+1)^2).");
            Ciphertext xPlusOneSq = new Ciphertext();
            _evaluator.AddPlain(xEncrypted, PlainOne, xPlusOneSq);
            _evaluator.SquareInplace(xPlusOneSq);
            return xPlusOneSq;
        }

        private Ciphertext XSqPlusOne(Ciphertext xEncrypted)
        {
            Console.WriteLine("Compute xSqPlusOne (x^2+1).");
            Ciphertext xSqPlusOne = new Ciphertext();
            _evaluator.Square(xEncrypted, xSqPlusOne);

           
            _evaluator.AddPlainInplace(xSqPlusOne, PlainOne);
            return xSqPlusOne;
        }

        private static SEALContext CreateContext()
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

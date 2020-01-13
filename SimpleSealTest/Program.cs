using Microsoft.Research.SEAL;
using System;


namespace SimpleSealTest
{
    class IrisSvmTrivial
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Utilities.PrintExampleBanner("My first use");

            EncryptionParameters parms = new EncryptionParameters(SchemeType.BFV);
            ulong polyModulusDegree = 4096;
            parms.PolyModulusDegree = polyModulusDegree;
            parms.CoeffModulus = CoeffModulus.BFVDefault(polyModulusDegree);
            parms.PlainModulus = new SmallModulus(1024);
            SEALContext context = new SEALContext(parms);
            Utilities.PrintLine();
            Console.WriteLine("Set encryption parameters and print");
                Utilities.PrintParameters(context);
            Console.WriteLine();
            Console.WriteLine("~~~~~~ A naive way to calculate 4(x^2+1)(x+1)^2. ~~~~~~");

            KeyGenerator keygen = new KeyGenerator(context);
            PublicKey publicKey = keygen.PublicKey;
            SecretKey secretKey = keygen.SecretKey;
            Console.WriteLine(publicKey.Data);
           
            
            Encryptor encryptor = new Encryptor(context, publicKey);

            //Server side
            Evaluator evaluator = new Evaluator(context);

            Decryptor decryptor = new Decryptor(context, secretKey);

            /*
                Plaintexts in the BFV scheme are:
                    polynomials of degree less than the degree of the polynomial modulus
                    and coefficients integers modulo the plaintext modulus
            */


            Utilities.PrintLine();
            int x = 6;
            Plaintext xPlain = new Plaintext(x.ToString());
            Console.WriteLine($"Express x = {x} as a plaintext polynomial 0x{xPlain}.");

            /*
            We then encrypt the plaintext, producing a ciphertext.
            */
            Utilities.PrintLine();
            Ciphertext xEncrypted = new Ciphertext();
            Console.WriteLine("Encrypt xPlain to xEncrypted.");
            encryptor.Encrypt(xPlain, xEncrypted);
            Console.WriteLine($"    + size of freshly encrypted x: {xEncrypted.Size}");

            /*
            There is plenty of noise budget left in this freshly encrypted ciphertext.
            */
            Console.WriteLine("    + noise budget in freshly encrypted x: {0} bits", decryptor.InvariantNoiseBudget(xEncrypted));


            Plaintext xDecrypted = new Plaintext();
            Console.Write("    + decryption of encrypted_x: ");
            decryptor.Decrypt(xEncrypted, xDecrypted);
            Console.WriteLine($"0x{xDecrypted} ...... Correct.");

            /*************************************
             compute(x +1)^2
            ****************************************/
            Utilities.PrintLine();
            Console.WriteLine("Compute xSqPlusOne (x^2+1).");
            Ciphertext xSqPlusOne = new Ciphertext();
            evaluator.Square(xEncrypted, xSqPlusOne);
            Console.WriteLine($"    + size of xSqPlusOne: {xSqPlusOne.Size}");
            Console.WriteLine("    + noise budget in xSqPlusOne: {0} bits", decryptor.InvariantNoiseBudget(xSqPlusOne));
            Plaintext plainOne = new Plaintext("1");
            evaluator.AddPlainInplace(xSqPlusOne, plainOne);
            Console.WriteLine($"    + size of xSqPlusOne: {xSqPlusOne.Size}");
            Console.WriteLine("    + >>>>>>>>>>> noise budget in xSqPlusOne: {0} bits", decryptor.InvariantNoiseBudget(xSqPlusOne));

            Plaintext decryptedResult = new Plaintext();
            Console.Write("    + decryption of xSqPlusOne: ");
            decryptor.Decrypt(xSqPlusOne, decryptedResult);
            Console.WriteLine($"0x{decryptedResult} ...... Correct.");



            /*
            Next, we compute (x + 1)^2.
            */
            Utilities.PrintLine();
            Console.WriteLine("Compute xPlusOneSq ((x+1)^2).");
            Ciphertext xPlusOneSq = new Ciphertext();
            evaluator.AddPlain(xEncrypted, plainOne, xPlusOneSq);
            evaluator.SquareInplace(xPlusOneSq);
            Console.WriteLine($"    + size of xPlusOneSq: {xPlusOneSq.Size}");
            Console.WriteLine("    + >>>>>> noise budget in xPlusOneSq: {0} bits", decryptor.InvariantNoiseBudget(xPlusOneSq));
            Console.Write("    + decryption of xPlusOneSq: ");
            decryptor.Decrypt(xPlusOneSq, decryptedResult);
            Console.WriteLine($"0x{decryptedResult} ...... Correct.");

            /*
            Finally, we multiply (x^2 + 1) * (x + 1)^2 * 4.
            */
            Utilities.PrintLine();
            Console.WriteLine("Compute encryptedResult (4(x^2+1)(x+1)^2).");
            Ciphertext encryptedResult = new Ciphertext();
            Plaintext plainFour = new Plaintext("4");
            evaluator.MultiplyPlainInplace(xSqPlusOne, plainFour);
            evaluator.Multiply(xSqPlusOne, xPlusOneSq, encryptedResult);
            Console.WriteLine($"    + size of encrypted_result: {encryptedResult.Size}");
            Console.WriteLine("    + noise budget in encrypted_result: {0} bits", decryptor.InvariantNoiseBudget(encryptedResult));
            Console.WriteLine("NOTE: Decryption can be incorrect if noise budget is zero.");
            decryptor.Decrypt(encryptedResult, decryptedResult);
            Console.WriteLine($"0x{decryptedResult} ...... ? FAILED."); // GALSAV: Sometimes the result is unexpexted cause the noise budget is 0  
            Console.WriteLine();


            // /Compute the same polinom evaluation but with relinearization


            Console.WriteLine("~~~~~~ A better way to calculate 4(x^2+1)(x+1)^2. ~~~~~~");

            Utilities.PrintLine();
            Console.WriteLine("Generate relinearization keys.");
            RelinKeys relinKeys = keygen.RelinKeys();

            /*
            We now repeat the computation relinearizing after each multiplication.
            */
            Utilities.PrintLine();
            Console.WriteLine("Compute and relinearize xSquared (x^2),");
            Console.WriteLine(new string(' ', 13) + "then compute xSqPlusOne (x^2+1)");
            Ciphertext xSquared = new Ciphertext();
            evaluator.Square(xEncrypted, xSquared);
            Console.WriteLine($"    + size of xSquared: {xSquared.Size}");
            evaluator.RelinearizeInplace(xSquared, relinKeys);
            Console.WriteLine("    + size of xSquared (after relinearization): {0}",
                xSquared.Size);
            evaluator.AddPlain(xSquared, plainOne, xSqPlusOne);
            Console.WriteLine("    + noise budget in xSqPlusOne: {0} bits",
                decryptor.InvariantNoiseBudget(xSqPlusOne));
            Console.Write("    + decryption of xSqPlusOne: ");
            decryptor.Decrypt(xSqPlusOne, decryptedResult);
            Console.WriteLine($"0x{decryptedResult} ...... Correct.");

            Utilities.PrintLine();
            Ciphertext xPlusOne = new Ciphertext();
            Console.WriteLine("Compute xPlusOne (x+1),");
            Console.WriteLine(new string(' ', 13) +
                "then compute and relinearize xPlusOneSq ((x+1)^2).");
            evaluator.AddPlain(xEncrypted, plainOne, xPlusOne);
            evaluator.Square(xPlusOne, xPlusOneSq);
            Console.WriteLine($"    + size of xPlusOneSq: {xPlusOneSq.Size}");
            evaluator.RelinearizeInplace(xPlusOneSq, relinKeys);
            Console.WriteLine("    + noise budget in xPlusOneSq: {0} bits",
                decryptor.InvariantNoiseBudget(xPlusOneSq));
            Console.Write("    + decryption of xPlusOneSq: ");
            decryptor.Decrypt(xPlusOneSq, decryptedResult);
            Console.WriteLine($"0x{decryptedResult} ...... Correct.");

            Utilities.PrintLine();
            Console.WriteLine("Compute and relinearize encryptedResult (4(x^2+1)(x+1)^2).");
            evaluator.MultiplyPlainInplace(xSqPlusOne, plainFour);
            evaluator.Multiply(xSqPlusOne, xPlusOneSq, encryptedResult);
            Console.WriteLine($"    + size of encryptedResult: {encryptedResult.Size}");
            evaluator.RelinearizeInplace(encryptedResult, relinKeys);
            Console.WriteLine("    + size of encryptedResult (after relinearization): {0}",
                encryptedResult.Size);
            Console.WriteLine("    + noise budget in encryptedResult: {0} bits",
                decryptor.InvariantNoiseBudget(encryptedResult));

            Console.WriteLine();
            Console.WriteLine("NOTE: Notice the increase in remaining noise budget.");

            /*
            Relinearization clearly improved our noise consumption. We have still plenty
            of noise budget left, so we can expect the correct answer when decrypting.
            */
            Utilities.PrintLine();
            Console.WriteLine("Decrypt encrypted_result (4(x^2+1)(x+1)^2).");
            decryptor.Decrypt(encryptedResult, decryptedResult);
            Console.WriteLine("    + decryption of 4(x^2+1)(x+1)^2 = 0x{0} ...... Correct.",
                decryptedResult);


        }
    }
}

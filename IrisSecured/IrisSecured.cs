using Microsoft.Research.SEAL;
using Microsoft.VisualBasic.FileIO;
using SecureSVC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace IrisSecured
{
    class IrisSecured
    {
        /*
		*  Class for a  client of Secured Iris classification
		*  The application loads samples from Iris dataset which is stored in the resource of the application
		*  A folder c:\Output must exist in order to properly run this application
		*/

        /*************************************************************
         * 
         *  Result - Class for parallel flow results :
         *   TotalValue - The decision value of calculated by the svm 
         *   Estimation - 0- first class , 1- second class
         * 
         * ************************************************************/
        class Result
	    {
		    public double   TotalValue;
		    public int		Estimation;

		    public Result(double totalValue, int estimation)
		    {
			    this.TotalValue = totalValue;
			    this.Estimation = estimation;
		    }
	    }

        private const string OutputDir = @"C:\Output\";
        /**************************************************
        * Two performance optimizations are posssible :
        *   1. use all core's
        *   2. do batch of input vectors , take advantage of SEAL batching
        *
        * ***************************************/
        private const Boolean IsParallel = false;

        static async Task Main(string[] args)
        {


            Console.WriteLine("Secure Iris");
            
            // Get Input from resource file or as args
            double[][] features;
            int numberOfFeatures = 4;
            int numOfRows = 0;

            // Option to load from args and not the whole dataset that is stored in resources
            if (args.Length >= numberOfFeatures)
            {
                numOfRows = args.Length / numberOfFeatures;
                // Features:
                features = new double[numOfRows][];
                for (int i = 0; i < numOfRows; i++)
                {
                    features[i] = new double[numberOfFeatures];
                }
                for (int i = 0, l = args.Length; i < l; i++)
                {
                    features[i / numberOfFeatures][i % numberOfFeatures] = Double.Parse(args[i]);
                }
            }
          
            else  // load the whole dataset from resources
            {
                List<double[]> rows = new List<double[]>();

                var bytes = Properties.Resources.iris;
                numOfRows = 0;
                features = SVCUtilities.SvcUtilities.LoadFeatures(bytes, numberOfFeatures, ref numOfRows);
            }
            Stopwatch clientStopwatch = new Stopwatch();
            clientStopwatch.Start();

            //svm algorithm parametrs calculated in python : training result
            double[][] vectors = new double[3][];

            vectors[0] = new[] { 5.1, 3.3, 1.7, 0.5 };
            vectors[1] = new[] { 4.8, 3.4, 1.9, 0.2 };
            vectors[2] = new[] { 5.1, 2.5, 3.0, 1.1 };

            double[][] coefficients = new double[1][];
            coefficients[0] = new double[] { -0.7407784813992192, -0.0025023664254470897, 0.7432808478246663 };
            double[] intercepts = { 0.9055182807973224 };


            // SEAL parameters client side
            Console.WriteLine("SecureSVC : ");

            EncryptionParameters parms = new EncryptionParameters(SchemeType.CKKS);

            ulong polyModulusDegree = 16384;
            int power = 40;

            double scale = Math.Pow(2.0, power);

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

            

            var context = new SEALContext(parms);
            // Key generation
            KeyGenerator keygen = new KeyGenerator(context);
            var publicKey = keygen.PublicKey;
            var secretKey = keygen.SecretKey;
            var relinKeys = keygen.RelinKeys();

            var galoisKeys = keygen.GaloisKeys();
            var encryptor = new Encryptor(context, publicKey);
           
           var  decryptor = new Decryptor(context, secretKey);
           var  encoder = new CKKSEncoder(context);

           clientStopwatch.Stop();


            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(
                   $@"{OutputDir}IrisSecured_{IsParallel}_{DateTime.Now.Day}_{DateTime.Now.ToShortTimeString().ToString().Replace(":", "_")}.txt")
            )
            {

				// Only CONCEPT demonstation how to parrallel all the computation on all machine cpu's 
				// Though the parallel here is done on the client side , in "real life" this parallel mechanism 
				// Should on the server side
                if ( IsParallel )
	            {
		            int processorCount = Environment.ProcessorCount;
		            Console.WriteLine("Number Of Logical Processors: {0}", processorCount);

		            Svc[] machines = new Svc[processorCount];

		            Stopwatch[] innerProductStopwatchArr	= new Stopwatch[processorCount];
		            Stopwatch[] negateStopwatchArr			= new Stopwatch[processorCount];
                    Stopwatch[] degreeStopwatchArr			= new Stopwatch[processorCount];
                    Stopwatch[] serverDecisionStopWatchArr = new Stopwatch[processorCount];
                    Result[] results = new Result[numOfRows];

                    Task[] tasks = new Task[processorCount];
		            for (int i = 0; i < processorCount; i++)
		            {
			            machines[i] = new Svc(vectors, coefficients, intercepts, "Linear", 0.25, 0.0, 3, 40, publicKey/*, secretKey*/, relinKeys, galoisKeys, 1, 4);
                        innerProductStopwatchArr[i]     = new Stopwatch();
                        negateStopwatchArr[i]           = new Stopwatch();
                        degreeStopwatchArr[i]           = new Stopwatch();
                        serverDecisionStopWatchArr[i]   = new Stopwatch();

                    }
		            Stopwatch totalTime = new Stopwatch();
		            totalTime.Start();
                    for (int i = 0; i < numOfRows;)
		            {

			            for (int j = 0; j < processorCount && i < numOfRows; j++)
			            {
				            var secureSvc = machines[i % processorCount];
				            var feature = features[i];
				            //Console.WriteLine($"\n\n $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
				            List<object> l = new List<object>();
				            l.Add(secureSvc);	//0
				            l.Add(feature);		//1
				            l.Add(i);			//2
				            l.Add(encoder);	//3
				            l.Add(encryptor);  //4
				            l.Add(decryptor);  //5
                            l.Add(innerProductStopwatchArr[i % processorCount]);
				            l.Add(degreeStopwatchArr[i % processorCount]);
				            l.Add(negateStopwatchArr[i % processorCount]);
				            l.Add(serverDecisionStopWatchArr[i % processorCount]);

                            l.Add(results);
				            tasks[j] = new TaskFactory().StartNew(new Action<object>((test) =>
				            {
					            List<object> l2 = (List<object>)test;
                               
					            SinglePredict((Svc)l2[0], (double[])l2[1], (int)l2[2], (CKKSEncoder)l2[3], (Encryptor)l2[4], (Decryptor)l2[5],(Stopwatch)l2[6], (Stopwatch)l2[7], (Stopwatch)l2[8], (Stopwatch)l2[9], scale,(Result[])l2[10]);

				            }), l);
				            i++;
			            }

			            await Task.WhenAll(tasks);

		            }

                    totalTime.Stop();

                    for (int i = 0; i < numOfRows; i++)
		            {
			            var result = results[i];
			            file.WriteLine($"{i} , {result.Estimation} , {result.TotalValue} ");

		            }

                    double innerProductTime = 0;
                    double degreeTime		= 0;
                    double negateTime		= 0;
                    double serverTime		= 0;

                    for (int i = 0; i < processorCount; i++)
                    {
	                    innerProductTime = innerProductStopwatchArr[i].ElapsedMilliseconds;
	                    degreeTime		 = degreeStopwatchArr[i].ElapsedMilliseconds;
	                    negateTime		 = negateStopwatchArr[i].ElapsedMilliseconds;
                        serverTime		 = serverDecisionStopWatchArr[i].ElapsedMilliseconds;
                    }
		            file.WriteLine($" Client time :  {clientStopwatch.ElapsedMilliseconds} ms  ");
		            file.WriteLine($" Total time for {numOfRows} samples :  {totalTime.ElapsedMilliseconds} ms  ");
		            file.WriteLine($" Avg time  :  {totalTime.ElapsedMilliseconds * 1000 / numOfRows} microSec ");
		            file.WriteLine($" Inner Product time for  {numOfRows} samples :  {innerProductTime} ms  ");
		            file.WriteLine($" Inner Product Avg time  :  {innerProductTime * 1000 / numOfRows} microSec ");
		            file.WriteLine($" Degree time for  {numOfRows} samples :  {degreeTime} ms  ");
		            file.WriteLine($" Degree Avg time  :  {degreeTime * 1000 / numOfRows} microSec ");
		            file.WriteLine($" Negate time for  {numOfRows} samples :  {negateTime} ms  ");
		            file.WriteLine($" Negate Avg time  :  {negateTime * 1000 / numOfRows} microSec ");
		            file.WriteLine($" Decision time for  {numOfRows} samples :  {serverTime} ms  ");
		            file.WriteLine($" Decision Avg time  :  {serverTime * 1000 / numOfRows} microSec ");

                }
                else
                {
					//Initiate Stopwatch for performance measure
	                Stopwatch innerProductStopwatch = new Stopwatch();
	                Stopwatch negateStopwatch = new Stopwatch();
	                Stopwatch degreeStopwatch = new Stopwatch();
	                Stopwatch serverDecisionStopWatch = new Stopwatch();

                    int featureSizeWithSpace = numberOfFeatures;
                    
                    int batchSize = 200;
                    if (batchSize>1)
                    {
                        featureSizeWithSpace = numberOfFeatures * 2;
                    }
                    
                    Svc clf = new Svc(vectors, coefficients, intercepts, "Linear", 0.25, 0.0, 3, 40, publicKey/*, secretKey*/, relinKeys, galoisKeys, batchSize,featureSizeWithSpace);
	                Stopwatch totalTime = new Stopwatch();
	                totalTime.Start();
                    int start = 0;
                    //double[] batchFeatures = new double[batchSize * featureSizeWithSpace];
                    for (int i = 0; i < numOfRows;)
	                {
                        start = i;
		                double finalResult = -10000;
                        double[][] batchRows  = new double[batchSize][];
                        for (int j= 0; j< batchSize && i < numOfRows; j++)
                        {

                            batchRows[j] = features[i];
                            i++;
                        }
                        double[] batchFeatures = GetBatchFeatures(batchRows, batchSize, numberOfFeatures, featureSizeWithSpace);


                        var plaintexts = new Plaintext();
		                var featuresCiphertexts = new Ciphertext();
		                encoder.Encode(batchFeatures, scale, plaintexts);
		                encryptor.Encrypt(plaintexts, featuresCiphertexts);

                        //Server side start
		                var cypherResult = clf.Predict(featuresCiphertexts, true, true, innerProductStopwatch, degreeStopwatch, negateStopwatch, serverDecisionStopWatch);
                        // Server side end
		                Plaintext plainResult = new Plaintext();
		                decryptor.Decrypt(cypherResult, plainResult);
		                List<double> result = new List<double>();
		                encoder.Decode(plainResult, result);

                        for (int j = 0; j < batchSize && start < numOfRows; j++)
                        {

                            finalResult = result[j* featureSizeWithSpace];
                            int estimation = finalResult > 0 ? 0 : 1;
                            Console.WriteLine($"\n ************************************************");
                            Console.WriteLine($"SVC estimation{i} is : {estimation} , result : {finalResult}");
                            file.WriteLine($"{start} , {estimation} , {finalResult} ");
                            Console.WriteLine($"************************************************ \n");
                            start++;
                        }



	                }
	                totalTime.Stop();
	                file.WriteLine($" Client time :  {clientStopwatch.ElapsedMilliseconds} ms  ");
	                file.WriteLine($" Total time for {numOfRows} samples :  {totalTime.ElapsedMilliseconds} ms  ");
	                file.WriteLine($" Avg time  :  {totalTime.ElapsedMilliseconds * 1000 / numOfRows} microSec ");
	                file.WriteLine($" Inner Product time for  {numOfRows} samples :  {innerProductStopwatch.ElapsedMilliseconds} ms  ");
	                file.WriteLine($" Inner Product Avg time  :  {innerProductStopwatch.ElapsedMilliseconds * 1000 / numOfRows} microSec ");
	                file.WriteLine($" Degree time for  {numOfRows} samples :  {degreeStopwatch.ElapsedMilliseconds} ms  ");
	                file.WriteLine($" Degree Avg time  :  {degreeStopwatch.ElapsedMilliseconds * 1000 / numOfRows} microSec ");
	                file.WriteLine($" Negate time for  {numOfRows} samples :  {negateStopwatch.ElapsedMilliseconds} ms  ");
	                file.WriteLine($" Negate Avg time  :  {negateStopwatch.ElapsedMilliseconds * 1000 / numOfRows} microSec ");
	                file.WriteLine($" Decision time for  {numOfRows} samples :  {serverDecisionStopWatch.ElapsedMilliseconds} ms  ");
	                file.WriteLine($" Decision Avg time  :  {serverDecisionStopWatch.ElapsedMilliseconds * 1000 / numOfRows} microSec ");
                }


            }



        }

        private static double[] GetBatchFeatures(double[][] batchRows, int batchSize, int featureSize, int featureSizeWithSpace)
        {
            double[] batchFeatures = new double[batchSize * featureSizeWithSpace];
            int k = 0;
            for(int i=0;i< batchSize;i++)
            {
                for(int j=0;j< featureSize;j++)
                {
                    if(batchRows[i]!=null)
                    { 
                        batchFeatures[k] = batchRows[i][j];
                    }
                    else
                    {
                        batchFeatures[k] = 0;
                    }
                    k++;
                }
                for(int r=0;r< featureSizeWithSpace - featureSize; r++)
                {
                    batchFeatures[k] = 0;
                    k++;
                }


            }
            return batchFeatures;

        }

		//
        private static void SinglePredict(Svc secureSvc, double[] feature, int i, CKKSEncoder encoder, Encryptor encryptor,Decryptor decryptor,
	        Stopwatch innerProductStopwatch, Stopwatch degreeStopwatch, Stopwatch negateStopwatch, Stopwatch serverDecisionStopWatch, double scale, Result[] results)
        {
	        double finalResult = 0;
	        Console.WriteLine($"start {i} \n");

	        var plaintexts = new Plaintext();
	        var featuresCiphertexts = new Ciphertext();
	        encoder.Encode(feature, scale, plaintexts);
	        encryptor.Encrypt(plaintexts, featuresCiphertexts);
            // Server side start
            var cyphetResult = secureSvc.Predict(featuresCiphertexts, true, true, innerProductStopwatch, degreeStopwatch, negateStopwatch, serverDecisionStopWatch);
            // Server side end
            //timePredictSum.Stop();
            Plaintext plainResult = new Plaintext();
            decryptor.Decrypt(cyphetResult, plainResult);
            List<double> result = new List<double>();
            encoder.Decode(plainResult, result);
            finalResult = result[0];
            int estimation = finalResult > 0 ? 0 : 1;
            Console.WriteLine($"\n ************************************************");
            Console.WriteLine($"SVC estimation{i} is : {estimation} , result : {finalResult}");
            //file.WriteLine($"{i} , {estimation} , {finalResult} ");
            Console.WriteLine($"************************************************ \n");
            results[i] = new Result(finalResult, estimation);
	        //Console.WriteLine($"SecureSVC estimation{i} is : {estimation} , finalResult = {finalResult} , Time = {timePredictSum.ElapsedMilliseconds}");

        }



    }
}

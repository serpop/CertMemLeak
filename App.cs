using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CertMemLeak
{
    public sealed class App
    {
        private static string CertPath;

        private static TimeSpan TestDuration;

        private static DateTime StartTime;

        private const int MaxConcurency = 50;

        private static SemaphoreSlim Semaphore;

        private static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                CertPath = args[0];
            }
            else
            {
                Console.WriteLine("Please specify certificate file name.");
                return 1;
            }

            if (args.Length > 1)
            {
                try
                {
                    TestDuration = XmlConvert.ToTimeSpan(args[1]);
                }
                catch (Exception xcp)
                {
                    Console.WriteLine($"Invalid test duration: {xcp.Message}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("Please specify test duration.");
                return 1;
            }

            StartTime = DateTime.UtcNow;

            Semaphore = new SemaphoreSlim(MaxConcurency);

            Run().Wait();

            return 0;
        }

        private static async Task Run()
        {
            for (int batchNo = 0; ; batchNo++)
            {
                await RunBatchAsync(batchNo);

                if (DateTime.UtcNow - StartTime > TestDuration)
                {
                    break;
                }
            }
        }

        private static async Task RunBatchAsync(int batchNo)
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 5000; i++)
            {
                Semaphore.Wait();

                tasks.Add(DoItAsync(batchNo, i));
            }

            await Task.WhenAll(tasks.ToArray());
        }

        private static Task DoItAsync(int batchNo, int iterationNo)
        {
            return Task.Run(() =>
            {
                try
                {
                    var elapsedTime = DateTime.UtcNow - StartTime;

                    try
                    {
                        using var clientCertificate = GetClientCertificate();

                        var chainPolicy = new X509ChainPolicy
                        {
                            RevocationFlag = X509RevocationFlag.ExcludeRoot,
                            RevocationMode = X509RevocationMode.NoCheck,
                            VerificationFlags = X509VerificationFlags.NoFlag,
                        };

                        using var chain = new X509Chain
                        {
                            ChainPolicy = chainPolicy,
                        };

                        var validationResult = chain.Build(clientCertificate);

                        if (validationResult)
                        {
                            CreatePrincipal(clientCertificate);
                        }

                        Console.WriteLine($"INF [{batchNo} {iterationNo} {elapsedTime}] {(validationResult ? "Valid" : "Invalid")}");
                    }
                    catch (Exception xcp)
                    {
                        Console.WriteLine($"ERR [{batchNo} {iterationNo} {elapsedTime}] {xcp.Message}");
                    }
                }
                finally
                {
                    Semaphore.Release();
                }
            });
        }

        private static X509Certificate2 GetClientCertificate()
        {
            var bytes = File.ReadAllBytes(CertPath);
            var cert = new X509Certificate2(bytes);
            return cert;
        }

        private static ClaimsPrincipal CreatePrincipal(X509Certificate2 certificate)
        {
            var claims = new List<Claim>();

            var issuer = certificate.Issuer;
            claims.Add(new Claim("issuer", issuer, ClaimValueTypes.String, "ClaimsIssuer"));

            var thumbprint = certificate.Thumbprint;
            claims.Add(new Claim(ClaimTypes.Thumbprint, thumbprint, ClaimValueTypes.Base64Binary, "ClaimsIssuer"));

            var value = certificate.SubjectName.Name;
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(ClaimTypes.X500DistinguishedName, value, ClaimValueTypes.String, "ClaimsIssuer"));
            }

            value = certificate.SerialNumber;
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(ClaimTypes.SerialNumber, value, ClaimValueTypes.String, "ClaimsIssuer"));
            }

            value = certificate.GetNameInfo(X509NameType.DnsName, false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(ClaimTypes.Dns, value, ClaimValueTypes.String, "ClaimsIssuer"));
            }

            value = certificate.GetNameInfo(X509NameType.SimpleName, false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(ClaimTypes.Name, value, ClaimValueTypes.String, "ClaimsIssuer"));
            }

            value = certificate.GetNameInfo(X509NameType.EmailName, false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(ClaimTypes.Email, value, ClaimValueTypes.String, "ClaimsIssuer"));
            }

            value = certificate.GetNameInfo(X509NameType.UpnName, false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(ClaimTypes.Upn, value, ClaimValueTypes.String, "ClaimsIssuer"));
            }

            value = certificate.GetNameInfo(X509NameType.UrlName, false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(ClaimTypes.Uri, value, ClaimValueTypes.String, "ClaimsIssuer"));
            }

            var identity = new ClaimsIdentity(claims, "Certificate");
            return new ClaimsPrincipal(identity);
        }
    }
}

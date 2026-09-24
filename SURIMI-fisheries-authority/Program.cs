using Eii.BlobStore;
using Eii.BlobStore.S3;
using EwECore.MSE;
using SURIMI.Common.gRPC;
using SURIMI.Common.gRPC.Services;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddServiceDefaults();

            builder.Services.AddSingleton<IBlobStore>(sp =>
            {
                // if AWS_ACCESS_KEY_ID is set, use S3 compatible storage
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
                {
                    return new S3BlobStore(
                        Environment.GetEnvironmentVariable("AWS_S3_ENDPOINT"),
                        Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
                        Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
                        Environment.GetEnvironmentVariable("AWS_BUCKET_NAME"),
                        inputBasePrefix: @"fisheries_authority", outputBasePrefix: @"fisheries_authority/output", localInputRoot: "Includes", localOutputRoot: "Output");
                }

                // Default local Filesystem
                return new LocalBlobStore(inputRoot: "Includes", outputRoot: "Output");
            });

            // Add services to the container.
            builder.Services.AddGrpc(options =>
            {
                options.Interceptors.Add<ExceptionMetadataInterceptor>();
                options.Interceptors.Add<VersionMetadataInterceptor>();
                options.MaxReceiveMessageSize = 100 * 1024 * 1024; // 16 MB
                options.MaxSendMessageSize = 100 * 1024 * 1024; // 16 MB
            });
            builder.Services.AddGrpcReflection();

            builder.Services.AddSingleton<ProtocolVersionService>();
            builder.Services.AddSingleton<SimulationScopeManager>();
            builder.Services.AddSingleton<QuotaShareLoader>();
            builder.Services.AddSingleton<RecruitmentLoader>();
            builder.Services.AddSingleton<InitialQuotaLoader>();
            builder.Services.AddScoped<IRandomService, cRandomService>();
            builder.Services.AddScoped<IMSEStockRecruitment, cMSEStockRecruitment>();
            builder.Services.AddScoped<IMSEQuotaCalculator, cMSEQuotaCalculator>();
            builder.Services.AddScoped<IMseDiagnosticsRecorder, CsvMseDiagnosticsRecorder>();
            builder.Services.AddScoped<IQuotaCalculationService, QuotaCalculationService>();

            builder.Logging.ClearProviders();
            builder.Services.AddLogging(opt =>
            {
                opt.AddSimpleConsole(c =>
                {
                    c.TimestampFormat = "[HH:mm:ss] ";
                });
            });

            var app = builder.Build();


            // Configure the HTTP request pipeline.
            app.MapGrpcService<FisheriesAuthorityService>();
            app.MapGrpcReflectionService();

            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            // Retrieve the logger
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            var protocolVersionService = app.Services.GetRequiredService<ProtocolVersionService>();
            var protocolVersion = protocolVersionService.LoadVersion();
            logger.LogInformation("Starting Fisheries Authority with protocol version {ProtocolVersion}", protocolVersion);

            LoadVaultSecretsInEnvironmentVariables();

            app.Run();
        }

        static void LoadVaultSecretsInEnvironmentVariables()
        {
            var vaultAddr = Environment.GetEnvironmentVariable("VAULT_ADDR");
            var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");
            var vaultTopDir = Environment.GetEnvironmentVariable("VAULT_TOP_DIR");
            var vaultRelativePath = Environment.GetEnvironmentVariable("VAULT_RELATIVE_PATH");
            var vaultMount = Environment.GetEnvironmentVariable("VAULT_MOUNT");
            if (string.IsNullOrEmpty(vaultAddr) || string.IsNullOrEmpty(vaultToken) || string.IsNullOrEmpty(vaultTopDir) || string.IsNullOrEmpty(vaultRelativePath) || string.IsNullOrEmpty(vaultMount))
            {
                Console.WriteLine("Vault Addr, Token, Top Dir, Relative Path, or Mount not set in environment variables. Skipping Vault loading.");
                return;
            }
            var vaultClient = new VaultSharp.VaultClient(new VaultSharp.VaultClientSettings(vaultAddr, new VaultSharp.V1.AuthMethods.Token.TokenAuthMethodInfo(vaultToken)));
            // Assuming secrets are stored under "secret/data/surimi"
            var secretPath = $"{vaultTopDir}/{vaultRelativePath}";
            try
            {
                var secret = vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(secretPath, mountPoint: vaultMount).Result;
                foreach (var kv in secret.Data.Data)
                {
                    Environment.SetEnvironmentVariable(kv.Key, kv.Value.ToString());
                    Console.WriteLine($"Loaded secret '{kv.Key}' from Vault into environment variables.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading secrets from Vault: {ex.Message}");
            }
        }
    }
}
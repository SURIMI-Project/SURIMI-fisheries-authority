using SURIMI_fisheries_authority.Services;
using SURIMI.Common.gRPC;
using SURIMI.Common.gRPC.Services;

namespace SURIMI_fisheries_authority
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddServiceDefaults();

            // Add services to the container.
            builder.Services.AddGrpc(options =>
            {
                options.Interceptors.Add<ExceptionMetadataInterceptor>();
                options.Interceptors.Add<VersionMetadataInterceptor>();
                options.MaxReceiveMessageSize = 100 * 1024 * 1024; // 16 MB
                options.MaxSendMessageSize = 100 * 1024 * 1024; // 16 MB
            });

            builder.Services.AddSingleton<ProtocolVersionService>();

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
            app.MapGrpcService<FisheriesAuthorityWorkflowService>();
            app.MapGrpcService<FisheriesAuthorityCatchConsumerService>();
            app.MapGrpcService<FisheriesAuthorityRegulationsProviderService>();

            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            // Retrieve the logger
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            var protocolVersionService = app.Services.GetRequiredService<ProtocolVersionService>();
            var protocolVersion = protocolVersionService.LoadVersion();
            logger.LogInformation("Starting Fisheries Authority with protocol version {ProtocolVersion}", protocolVersion);

            app.Run();
        }
    }
}
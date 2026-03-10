using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Diagnostics;

namespace SURIMI_fisheries_authority
{
    public class ExceptionMetadataInterceptor : Interceptor
    {
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (RpcException)
            {
                // If it's a known RpcException, we don't need to do anything special
                // meta data is allready set
                throw;
            }
            catch (Exception ex)
            {
                LogExceptionToActivity(ex);

                var status = new Status(StatusCode.Internal, ex.Message);
                var metadata = new Metadata
                {
                    { "method", context.Method },
                    { "application", "EwE" }
                };
                throw new RpcException(status, metadata);
            }
        }


        private void LogExceptionToActivity(Exception ex)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().ToString() },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.StackTrace ?? string.Empty }
                }));
            }
        }
    }

}

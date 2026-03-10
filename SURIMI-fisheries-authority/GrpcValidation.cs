using System.Runtime.CompilerServices;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;

namespace SURIMI_fisheries_authority
{
    public class GrpcValidation
    {
        public static void ArgumentNotNullOrEmpty(string value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                var status = new Google.Rpc.Status
                {
                    Code = (int)Code.InvalidArgument,
                    Message = "Bad request",
                    Details =
                {
                    Any.Pack(new BadRequest
                    {
                        FieldViolations =
                        {
                            new BadRequest.Types.FieldViolation
                            {
                                Field = paramName,
                                Description = "Value is null or empty"
                            }
                        }
                    })
                }
                };
                throw status.ToRpcException();
            }
        }
    }
}

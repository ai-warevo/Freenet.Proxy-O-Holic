using System.Net;

namespace AiWarevo.Freenet.ProxyOHolic;

public record ProxySettings(
    int Port = 8118, 
    int BufferSize = 8192
)
{
    public IPAddress ListenAddress => IPAddress.Any;
}

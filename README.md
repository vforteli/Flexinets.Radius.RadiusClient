# Radius client library for .NET Standard

This library can be used to asynchronously send packets to Radius servers

## RadiusClient usage

```csharp
using var client = new RadiusClient(
    new IPEndPoint(IPAddress.Any, 58733),
    new RadiusPacketParser(
        loggerFactory.CreateLogger<RadiusPacketParser>(),
        RadiusDictionary.Parse(DefaultDictionary.RadiusDictionary)));


var requestPacket = new RadiusPacket(PacketCode.AccessRequest, 0, "xyzzy5461");
requestPacket.AddMessageAuthenticator(); // Add message authenticator for BLASTRadius
requestPacket.AddAttribute("User-Name", "nemo");
requestPacket.AddAttribute("User-Password", "arctangent");

var responsePacket = await client.SendPacketAsync(
    requestPacket,
    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1812));

if (responsePacket.Code == PacketCode.AccessAccept)
{
    // Hooray
}
```

Multiple requests and responses can be made asynchronously on the same local port as long as the identifier and remote host:port remain unique

https://www.nuget.org/packages/Flexinets.Radius.RadiusClient/

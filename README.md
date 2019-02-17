# Radius client library for .NET Standard

This library can be used to asynchronously send packets to Radius servers

## RadiusClient usage    
```
var dictionary = new RadiusDictionary(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\radius.dictionary");
var radiusPacketParser = new RadiusPacketParser(NullLogger<RadiusPacketParser>.Instance, dictionary)
var client = new RadiusClient(new IPEndPoint(IPAddress.Any, 1824), radiusPacketParser);

var packet = new RadiusPacket(PacketCode.AccessRequest, 0, "xyzzy5461");
packet.AddAttribute("User-Name", "nemo");
packet.AddAttribute("User-Password", "arctangent");
packet.AddAttribute("NAS-IP-Address", IPAddress.Parse("192.168.1.16"));
packet.AddAttribute("NAS-Port", 3);

var responsePacket = await client.SendPacketAsync(packet, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1812), TimeSpan.FromSeconds(11));
if (responsePacket.Code == PacketCode.AccessAccept)
{
  // Hooray          
}
```

Multiple requests and responses can be made asynchronously on the same local port as long as the identifier and remote host:port remain unique  

https://www.nuget.org/packages/Flexinets.Radius.RadiusClient/

using SocketChat.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<SocketServer>();

var host = builder.Build();
host.Run();

This is a basic implementation of the SMPP protocol using modern c#.
### SMPP Client
You can use the SmppClient type with zero knowledge of the SMPP protocol, the main features of a client are connecting to an SMSC, sending messages and receiving delivery receipts

* SMPP sessions are supposed to be long-lived, in case you need to connect to an SMPP server from an Asp.Net Core web application, it is recommended to wrap the SmppClient instances within a singleton service to avoid opening multiple sessions for many requests.

```

var client = new SmppClient("127.0.0.1", 45562)
{
    SystemId = "test",
    Password = "test"
};
client.OnSegmentSent += OnSegmentSent;
client.OnSegmentDelivered += OnSegmentDelivered;

await client.ConnectAsync();

var sms = new TextMessage
{
    Source = "11111",
    Destination = "22222",
    Text = "Text",
    DataCoding = DataCoding.Default,
    RegisterDelivery = true
};
await client.SendMessageAsync(sms);


private static void OnSegmentDelivered(object? sender, SegmentDeliveredArgs e)
{
    Console.WriteLine(e.DeliveryReceipt);
}

private static void OnSegmentSent(object? sender, SegmentSentArgs e)
{
    Console.WriteLine($"Message sent, ReferenceId={e.ReferenceId}, MessageId={e.MessageId}");
}
```


### SMPP Server
When writing server code, unlike writing client code you need to have basic understanding of the SMPP protocol, this is in order to send valid response PDUs with proper command_status. The SmppServer type provides you with events to bind ESMEs and get notified when they submit message. Also there are methods to send delivery receipts back to clients.
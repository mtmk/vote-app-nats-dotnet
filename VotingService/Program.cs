﻿using Common;
using NATS.Client.Core;
using NATS.Client.Services;

// Set the custom serializer registry as the default for the connection.
var serializerRegistry = new NatsJsonContextSerializerRegistry(CandidateDataContext.Default);

var natsConnectionOptions = NatsOpts.Default with { SerializerRegistry = serializerRegistry };

await using var connection = new NatsConnection(natsConnectionOptions);

// Register as service with NATS
var svc = new NatsSvcContext(connection);
await using var service = await svc.AddServiceAsync(new("vote", "1.0.0")
{
    Description = "Casts vote for candidate",
});

// Receiver for vote.send
await service.AddEndpointAsync<object>(HandleMessage, "send", "vote.send.*");

Console.WriteLine("Voting Service is ready.");
Console.ReadKey();
return;

async ValueTask HandleMessage(NatsSvcMsg<object> msg)
{
    var candidateId = Convert.ToInt32(msg.Subject.Split('.')[^1]);
    Console.WriteLine($"Received vote for candidate: {candidateId}");

    // Retrieve the candidate IDs from the Candidate Service
    var candidateResponse = await connection.RequestAsync<object, Dictionary<int, string>>("candidate.get", null);
    var candidates = candidateResponse.Data?.Keys.ToList() ?? [];

    // Validate the candidate ID
    if (!candidates.Contains(candidateId))
    {
        await msg.ReplyAsync("Invalid candidate ID");
    }
    else
    {
        //Publish the vote to the Vote Processor service
        await connection.PublishAsync("vote.save", candidateId);
        await msg.ReplyAsync("Vote has been cast");
    }

    Console.WriteLine("Vote processed");
}
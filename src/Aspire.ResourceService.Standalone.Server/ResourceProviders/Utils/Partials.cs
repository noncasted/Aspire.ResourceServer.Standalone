using Aspire.Dashboard.Model;
using Docker.DotNet.Models;
using Google.Protobuf.WellKnownTypes;
using Aspire.ResourceService.Standalone.Server.ResourceProviders.K8s.Models;

// ReSharper disable CheckNamespace

namespace Aspire.ResourceService.Proto.V1;

public sealed partial class Resource
{
    internal static Resource FromDockerContainer(ContainerListResponse container)
    {
        var containerName = container.Names.First().Replace("/", "");
        var resource = new Resource
        {
            CreatedAt = Timestamp.FromDateTime(container.Created),
            State = MapDockerState(container.State),
            DisplayName = containerName,
            ResourceType = KnownResourceTypes.Container,
            Name = containerName,
            Uid = container.ID
        };
        resource.Urls.Add(container.Ports.Where(p => !string.IsNullOrEmpty(p.IP))
            .Select(s => new Url
            {
                IsInternal = false,
                EndpointName = $"http://{s.IP}:{s.PublicPort}",
                FullUrl = $"http://{s.IP}:{s.PublicPort}",
                DisplayProperties = new()
                {
                    SortOrder = 0,
                    DisplayName = ""
                }
            }));
        return resource;
    }
    // Docker returns states like "running" / "exited" (lowercase), whereas the Aspire dashboard
    // expects KnownResourceStates values ("Running", "Exited", ...) for correct status icons.
    private static string MapDockerState(string? dockerState) => dockerState?.ToLowerInvariant() switch
    {
        "running" or "paused" => "Running",
        "created" => "NotStarted",
        "restarting" => "Starting",
        "removing" => "Stopping",
        "exited" => "Exited",
        "dead" => "Failed",
        _ => dockerState ?? "Unknown"
    };

    internal static Resource FromK8sContainer(KubernetesContainer container)
    {
        var resource = new Resource
        {
            CreatedAt = container.StartedAt,
            State = container.State,
            DisplayName = container.Name,
            ResourceType = KnownResourceTypes.Container,
            Name = container.Name,
            Uid = container.ContainerID
        };
        resource.Urls.Add(new Url()
        {
            IsInternal = false,
            EndpointName = $"http://{container.Name}:{container.Port}",
            FullUrl = $"http://{container.Name}:{container.Port}",
            DisplayProperties = new()
            {
                SortOrder = 0,
                DisplayName = ""
            }
        });
        return resource;
    }
}

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
        // Prefer the compose service label ("silo", "migrator", ...) when present —
        // much cleaner than the docker-generated "<project>-<service>-<N>".
        var displayName = container.Labels != null
                          && container.Labels.TryGetValue("com.docker.compose.service", out var svc)
                          && !string.IsNullOrWhiteSpace(svc)
            ? svc
            : containerName;

        var resource = new Resource
        {
            CreatedAt = Timestamp.FromDateTime(container.Created),
            State = MapDockerState(container.State, container.Status),
            DisplayName = displayName,
            ResourceType = KnownResourceTypes.Container,
            Name = displayName,
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
    // For exited containers, we inspect the Status string ("Exited (0) 5 minutes ago") to tell
    // graceful exits (init containers, one-shot jobs) from crashes.
    private static string MapDockerState(string? dockerState, string? dockerStatus) => dockerState?.ToLowerInvariant() switch
    {
        "running" or "paused" => "Running",
        "created" => "NotStarted",
        "restarting" => "Starting",
        "removing" => "Stopping",
        "exited" => TryParseExitCode(dockerStatus) == 0 ? "Finished" : "Failed",
        "dead" => "Failed",
        _ => dockerState ?? "Unknown"
    };

    private static int? TryParseExitCode(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var open = status.IndexOf('(');
        var close = status.IndexOf(')');
        if (open < 0 || close <= open) return null;
        return int.TryParse(status.AsSpan(open + 1, close - open - 1), out var code) ? code : null;
    }

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

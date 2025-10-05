using k8s;

namespace IntegrationPlatform.Engine.Applications.Kubernetes;

public static class K8sClientFactory
{
    public static IKubernetes CreateClientK8S()
    {
        var config = KubernetesClientConfiguration.InClusterConfig();
        return new k8s.Kubernetes(config);
    }
}
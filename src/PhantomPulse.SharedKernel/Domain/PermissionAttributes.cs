namespace PhantomPulse.SharedKernel.Domain;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

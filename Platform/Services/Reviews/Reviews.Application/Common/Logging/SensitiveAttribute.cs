namespace Reviews.Application.Common.Logging;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SensitiveAttribute : Attribute { }
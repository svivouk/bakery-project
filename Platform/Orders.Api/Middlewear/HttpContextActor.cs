using Orders.Dal;

namespace Orders.Api.Middlewear
{
    public sealed class HttpContextActor(IHttpContextAccessor accessor) : ICurrentActor
    {
        private const int MaxLength = 128;   

        public string Name
        {
            get
            {
                var name = accessor.HttpContext?.User.Identity?.Name;
                if (string.IsNullOrWhiteSpace(name)) return "api:orders";
                return name.Length <= MaxLength ? name : name[..MaxLength];
            }
        }
    }
}

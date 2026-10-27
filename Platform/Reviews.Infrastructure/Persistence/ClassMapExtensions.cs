using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace Reviews.Infrastructure.Persistence;

internal static class ClassMapExtensions
{
    private static readonly CamelCaseElementNameConvention CamelCase = new();
    public static BsonMemberMap MapCamel<TClass, TMember>(
        this BsonClassMap<TClass> classMap, Expression<Func<TClass, TMember>> member)
    {
        var memberMap = classMap.MapProperty(member);
        CamelCase.Apply(memberMap);
        return memberMap;
    }
}
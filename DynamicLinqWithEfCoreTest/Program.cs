using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace DynamicLinqWithEfCoreTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var list = new List<Person>
            {
                new Person
                {
                    Age = 1,
                    EnglishName = "sadfasdf",
                    Id = Guid.NewGuid(),
                    Name = "tom"
                },
                new Person
                {
                    Age = 2,
                    EnglishName = "ddddd",
                    Id = Guid.NewGuid(),
                    Name = "jerry"
                }
            };

            using (var context = new TestContext())
            {
                try
                {
                    //DynamicExpressionParser.ParseLambda(new ParsingConfig { UseDynamicObjectClassForAnonymousTypes = true }, null, "new {1 AS Id}");

                    context.Database.EnsureCreated();

                    context.Set<Person>().AddRange(list);
                    context.SaveChanges();

                    context.Set<Person>().Select(d => new Tuple<int>(d.Age)).GroupBy(d => new Tuple<int>(d.Item1)).Select("new {Key.Item1, Sum(Item1) As Value}").TryLogToList("exp: Tuple,Tuple");
                    context.Set<Person>().Select(d => new { Id = d.Age }).GroupBy(d => new { Id = d.Id }).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("exp: Anonymous,Anonymous");
                    context.Set<Person>().Select(d => new { Id = d.Age }).GroupBy(d => new Tmp1 { Id = d.Id }).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("exp: Anonymous,Tmp1; should failed");
                    context.Set<Person>().Select(d => new { Id = d.Age }).GroupBy(d => new Tmp2 { Id = d.Id }).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("exp: Anonymous,Tmp2; should failed");
                    context.Set<Person>().Select(d => new Tmp1 { Id = d.Age }).GroupBy(d => new { Id = d.Id }).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("exp: Tmp1,Anonymous; should failed");
                    context.Set<Person>().Select(d => new Tmp2 { Id = d.Age }).GroupBy(d => new { Id = d.Id }).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("exp: Tmp2,Anonymous; should failed");

                    context.Set<Person>().Select("new { Age AS Id }").GroupBy("new { Id }").Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("dyn: Anonymous,Anonymous");
                    //Note the changed in #117 for Anonymous Type is also needed
                    context.Set<Person>().Select4Test("new { Age AS Id }", true).GroupBy4Test("new { Id }", true).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("dyn: Anonymous,Anonymous; with createParameterCtor=true");
                    context.Set<Person>().Select4Test("new { Age AS Id }", false).GroupBy4Test("new { Id }", false).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("dyn: Anonymous,Anonymous; with createParameterCtor=false");

                    context.Set<Person>().Select<Tmp1>("new { Age AS Id }").GroupBy(DynamicExpressionParser.ParseLambda<Tmp1, Tmp1>(null, false, "new { Id }")).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("dyn: Tmp1,Tmp1(without ParameterCtor)");
                    context.Set<Person>().Select<Tmp2>("new { Age AS Id }").GroupBy(DynamicExpressionParser.ParseLambda<Tmp2, Tmp2>(null, false, "new { Id }")).Select("new {Key.Id, Sum(Id) As Value}").TryLogToList("dyn: Tmp2,Tmp2(with ParameterCtor)");
                }
                finally
                {
                    context.Database.EnsureDeleted();
                }
            }

            Console.WriteLine("Press any key to quit");
            Console.ReadKey();
        }
        //one class without createParameterCtor 
        public class Tmp1
        {
            public Tmp1()
            {
            }
            public int Id { get; set; }
        }
        //one class with createParameterCtor 
        public class Tmp2
        {
            public Tmp2()
            {
            }
            public Tmp2(int id)
            {
            }
            public int Id { get; set; }
        }
    }

    internal static class TestUtils
    {
        public static IQueryable Select4Test(this IQueryable source, string selector, bool createParameterCtor)
        {
            LambdaExpression lambda = DynamicExpressionParser.ParseLambda(null, createParameterCtor, source.ElementType, null, selector);

            var optimized = Expression.Call(
                typeof(Queryable), nameof(Queryable.Select),
                new[] { source.ElementType, lambda.Body.Type },
                source.Expression, Expression.Quote(lambda));

            return source.Provider.CreateQuery(optimized);
        }

        public static IQueryable GroupBy4Test(this IQueryable source, string keySelector, bool createParameterCtor)
        {
            var keyLambda = DynamicExpressionParser.ParseLambda(null, createParameterCtor, source.ElementType, null, keySelector);

            var optimized = Expression.Call(
                typeof(Queryable), nameof(Queryable.GroupBy),
                new[] { source.ElementType, keyLambda.Body.Type }, source.Expression, Expression.Quote(keyLambda));

            return source.Provider.CreateQuery(optimized);
        }

        public static List<dynamic> TryLogToList(this IQueryable queryable, string tag)
        {
            try
            {
                var res = queryable.ToDynamicList();
                Console.Error.WriteLine($"Success query with {tag}." + Environment.NewLine);
                return res;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed query with {tag}:" + Environment.NewLine + e.Message + Environment.NewLine);
                return null;
            }
        }
    }

    public class Person
    {
        public Guid Id { get; set; }
        public int Age { get; set; }
        public string Name { get; set; }
        public string EnglishName { get; set; }
    }

    public class TestContext : DbContext
    {
        public TestContext()
        {

        }

        public TestContext(DbContextOptions<TestContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=Test165;Trusted_Connection=True;")
                .ConfigureWarnings(warns=>warns.Throw(RelationalEventId.QueryClientEvaluationWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(m => m.Id);
        }
    }
}

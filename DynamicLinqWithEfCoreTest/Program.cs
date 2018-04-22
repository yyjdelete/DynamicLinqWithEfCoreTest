using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Dynamic.Core;

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
                context.Set<Person>().AddRange(list);
                context.SaveChanges();
            }

            const int num = 100_000;

            var sw = Stopwatch.StartNew();

            for (var i = 0; i < num; i++)
            {
                using (var context = new TestContext())
                {
                    var set = context.Set<Person>().Where(m => m.Age == 1 && m.Name == "tom").ToList();
                }
            }

            Console.WriteLine(sw.Elapsed);

            sw.Restart();

            for (var i = 0; i < num; i++)
            {
                using (var context = new TestContext())
                {
                    var set = context.Set<Person>().Where("$.Age==@0 and $.Name==@1", 1, "tom").ToList();
                }
            }

            Console.WriteLine(sw.Elapsed);

            //Console.WriteLine("Press any key to quit");
            //Console.ReadKey();
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
            optionsBuilder.UseInMemoryDatabase("test");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(m => m.Id);
        }
    }
}

using FluentAssertions;
using Mapster;
using MajdsApp.SharedKernel.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Unit;

/// <summary>P4 FR-XC-007: one mapping configuration for the application, found per module, used by handlers instead of hand-written copies.</summary>
public class ObjectMapperTests
{
    public record Person(int Id, string Name, DateTime Born, PersonKind Kind);
    public enum PersonKind { Staff, Guest }
    public record PersonDto(int Id, string Name, DateTime Born, string Kind);
    public record PersonCard(string Title);

    /// <summary>What a module writes when a DTO cannot be matched by name alone.</summary>
    public class PersonMapping : IRegister
    {
        public void Register(TypeAdapterConfig config) =>
            config.NewConfig<Person, PersonCard>().Map(d => d.Title, s => s.Name + " (" + s.Kind + ")");
    }

    private static IObjectMapper Mapper() =>
        new ServiceCollection().AddPlatformMapping(typeof(ObjectMapperTests).Assembly).BuildServiceProvider().GetRequiredService<IObjectMapper>();

    [Fact]
    public void A_dto_whose_members_match_needs_no_mapping_code_and_an_enum_becomes_its_name()
    {
        var dto = Mapper().Map<PersonDto>(new Person(7, "Ada", new DateTime(1815, 12, 10), PersonKind.Guest));

        dto.Should().Be(new PersonDto(7, "Ada", new DateTime(1815, 12, 10), "Guest"));
    }

    [Fact]
    public void A_mapping_a_module_registered_is_found_by_the_scan()
    {
        Mapper().Map<PersonCard>(new Person(1, "Grace", DateTime.UnixEpoch, PersonKind.Staff)).Title.Should().Be("Grace (Staff)");
    }

    [Fact]
    public void The_projection_is_an_expression_a_query_can_translate_and_it_gives_the_same_result()
    {
        var mapper = Mapper();
        var source = new[] { new Person(1, "A", DateTime.UnixEpoch, PersonKind.Staff), new Person(2, "B", DateTime.UnixEpoch, PersonKind.Guest) };

        var viaQuery = source.AsQueryable().Select(mapper.Projection<Person, PersonDto>()).ToList();

        viaQuery.Select(p => p.Kind).Should().Equal("Staff", "Guest");
        mapper.Projection<Person, PersonDto>().Should().BeSameAs(mapper.Projection<Person, PersonDto>()); // built once
    }
}

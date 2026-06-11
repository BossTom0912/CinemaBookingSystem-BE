// Global using directives for the CinemaSystem.Tests project.
// The test files import entities from CinemaSystem.Infrastructure.Persistence.Models
// but the actual entities live in CinemaSystem.Domain.Entities.
// We expose the Domain entities under the expected namespace alias so all test files
// compile without modification.
global using CinemaSystem.Domain.Entities;

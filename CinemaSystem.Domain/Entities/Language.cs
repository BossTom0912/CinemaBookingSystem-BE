using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Language
{
    public string LanguageId { get; set; } = null!;
    public string Name { get; set; } = null!;

    public virtual ICollection<Movie> Movies { get; set; } = new List<Movie>();
}

// Test harness support. Exposes IceBot's internal types to the harness test project so
// pure-logic classes can be unit-tested without making them public.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("IceBot.Harness.Tests")]

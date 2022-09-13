using System;

namespace OctoshiftCLI;

public class DateTimeProvider
{
  public virtual long CurrentUnixTimeSeconds() => DateTimeOffset.Now.ToUnixTimeSeconds(); 
}
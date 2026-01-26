using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Babelserver.DotNet.TestLogger;

[ExtensionUri("logger://Babelserver.DotNet.TestLogger.ListAll/v1")]
[FriendlyName("listall")]
public class ListAllTestLogger : ListTestLogger
{
    protected override bool DefaultVerbose => true;
}

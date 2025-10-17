using AI_AI_Agent.Domain.CodeInterpreter;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AI_AI_Agent.Infrastructure.Services.CodeInterpreter
{
    /// <summary>
    /// Advanced code interpreter service with execution, debugging, and data analysis
    /// </summary>
    public class CodeInterpreterService
    {
        private readonly ILogger<CodeInterpreterService> _logger;
        private readonly ConcurrentDictionary<string, CodeExecution> _executions = new();
        private readonly ConcurrentDictionary<string, DebugSession> _debugSessions = new();
        private readonly ConcurrentDictionary<string, List<PackageInfo>> _installedPackages = new();
        private readonly string _workDir;

        public CodeInterpreterService(ILogger<CodeInterpreterService> logger)
        {
            _logger = logger;
            _workDir = Path.Combine(Directory.GetCurrentDirectory(), "code_execution");
            Directory.CreateDirectory(_workDir);
        }

        #region Code Execution

        public async Task<CodeExecution> ExecuteCodeAsync(
            string code,
            string language,
            ExecutionMode mode = ExecutionMode.Standard,
            CancellationToken cancellationToken = default)
        {
            var execution = new CodeExecution
            {
                Code = code,
                Language = language,
                Mode = mode,
                Status = ExecutionStatus.Running
            };

            _executions[execution.Id] = execution;

            try
            {
                _logger.LogInformation("Executing {Language} code in {Mode} mode", language, mode);

                switch (language.ToLower())
                {
                    case "python":
                        await ExecutePythonAsync(execution, cancellationToken);
                        break;
                    case "javascript":
                    case "js":
                        await ExecuteJavaScriptAsync(execution, cancellationToken);
                        break;
                    case "csharp":
                    case "cs":
                        await ExecuteCSharpAsync(execution, cancellationToken);
                        break;
                    default:
                        execution.Status = ExecutionStatus.Failed;
                        execution.Error = $"Unsupported language: {language}";
                        break;
                }

                execution.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Code execution {ExecutionId} completed with status {Status}",
                    execution.Id, execution.Status);
            }
            catch (Exception ex)
            {
                execution.Status = ExecutionStatus.Failed;
                execution.Error = ex.Message;
                _logger.LogError(ex, "Code execution {ExecutionId} failed", execution.Id);
            }

            return execution;
        }

        private async Task ExecutePythonAsync(CodeExecution execution, CancellationToken cancellationToken)
        {
            var scriptPath = Path.Combine(_workDir, $"{execution.Id}.py");
            await File.WriteAllTextAsync(scriptPath, execution.Code, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _workDir
            };

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            execution.Output = output.ToString();
            if (process.ExitCode == 0)
            {
                execution.Status = ExecutionStatus.Completed;
            }
            else
            {
                execution.Status = ExecutionStatus.Failed;
                execution.Error = error.ToString();
            }

            // Clean up
            try { File.Delete(scriptPath); } catch { }
        }

        private async Task ExecuteJavaScriptAsync(CodeExecution execution, CancellationToken cancellationToken)
        {
            var scriptPath = Path.Combine(_workDir, $"{execution.Id}.js");
            await File.WriteAllTextAsync(scriptPath, execution.Code, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _workDir
            };

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            execution.Output = output.ToString();
            if (process.ExitCode == 0)
            {
                execution.Status = ExecutionStatus.Completed;
            }
            else
            {
                execution.Status = ExecutionStatus.Failed;
                execution.Error = error.ToString();
            }

            // Clean up
            try { File.Delete(scriptPath); } catch { }
        }

        private Task ExecuteCSharpAsync(CodeExecution execution, CancellationToken cancellationToken)
        {
            // Simplified C# execution - in production would use Roslyn scripting
            execution.Output = "C# execution requires Roslyn scripting API integration";
            execution.Status = ExecutionStatus.Completed;
            return Task.CompletedTask;
        }

        public CodeExecution? GetExecution(string executionId)
        {
            return _executions.TryGetValue(executionId, out var execution) ? execution : null;
        }

        #endregion

        #region Data Analysis

        public async Task<DataAnalysisResult> AnalyzeDataAsync(string dataPath, string analysisType = "summary")
        {
            var result = new DataAnalysisResult
            {
                DatasetName = Path.GetFileName(dataPath)
            };

            try
            {
                // Read and analyze data
                var lines = await File.ReadAllLinesAsync(dataPath);
                var headers = lines[0].Split(',');

                result.Statistics = new DataStatistics
                {
                    RowCount = lines.Length - 1,
                    ColumnCount = headers.Length,
                    Columns = headers.Select(h => new ColumnInfo
                    {
                        Name = h.Trim(),
                        DataType = "string"
                    }).ToList()
                };

                // Generate insights
                result.Insights.Add(new Insight
                {
                    Title = "Dataset Overview",
                    Description = $"Dataset contains {result.Statistics.RowCount} rows and {result.Statistics.ColumnCount} columns",
                    Type = InsightType.Pattern,
                    Confidence = 1.0
                });

                result.Summary = $"Analyzed {result.DatasetName} with {result.Statistics.RowCount} records";

                _logger.LogInformation("Completed data analysis for {DatasetName}", result.DatasetName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze data from {DataPath}", dataPath);
                result.Summary = $"Error: {ex.Message}";
            }

            return result;
        }

        public async Task<Visualization> CreateVisualizationAsync(
            string dataPath,
            VisualizationType type,
            string xColumn,
            string yColumn)
        {
            var visualization = new Visualization
            {
                Title = $"{type} - {yColumn} vs {xColumn}",
                Type = type,
                Description = $"Visualization created from {Path.GetFileName(dataPath)}"
            };

            // In production, would integrate with plotting libraries (matplotlib, plotly, etc.)
            _logger.LogInformation("Created {Type} visualization", type);

            return visualization;
        }

        #endregion

        #region Interactive Debugging

        public DebugSession StartDebugSession(string executionId)
        {
            var session = new DebugSession
            {
                ExecutionId = executionId,
                State = DebugState.NotStarted
            };

            _debugSessions[session.Id] = session;
            _logger.LogInformation("Started debug session {SessionId} for execution {ExecutionId}",
                session.Id, executionId);

            return session;
        }

        public void AddBreakpoint(string sessionId, int lineNumber, string? condition = null)
        {
            if (_debugSessions.TryGetValue(sessionId, out var session))
            {
                session.Breakpoints.Add(new Breakpoint
                {
                    LineNumber = lineNumber,
                    Condition = condition,
                    IsEnabled = true
                });

                _logger.LogDebug("Added breakpoint at line {LineNumber} in session {SessionId}",
                    lineNumber, sessionId);
            }
        }

        public void StepOver(string sessionId)
        {
            if (_debugSessions.TryGetValue(sessionId, out var session))
            {
                session.State = DebugState.StepOver;
                _logger.LogDebug("Step over in debug session {SessionId}", sessionId);
            }
        }

        public void StepInto(string sessionId)
        {
            if (_debugSessions.TryGetValue(sessionId, out var session))
            {
                session.State = DebugState.StepInto;
                _logger.LogDebug("Step into in debug session {SessionId}", sessionId);
            }
        }

        public List<Variable> GetVariables(string sessionId)
        {
            if (_debugSessions.TryGetValue(sessionId, out var session))
            {
                return session.Variables;
            }
            return new List<Variable>();
        }

        #endregion

        #region Package Management

        public async Task<PackageInfo> InstallPackageAsync(string packageName, string language, string? version = null)
        {
            var package = new PackageInfo
            {
                Name = packageName,
                Version = version ?? "latest",
                IsInstalled = false,
                IsSafe = true
            };

            try
            {
                // Security check
                if (!IsPackageSafe(packageName))
                {
                    package.IsSafe = false;
                    package.SecurityIssue = "Package flagged as potentially unsafe";
                    _logger.LogWarning("Package {PackageName} flagged as unsafe", packageName);
                    return package;
                }

                // Install based on language
                var success = language.ToLower() switch
                {
                    "python" => await InstallPythonPackageAsync(packageName, version),
                    "javascript" or "js" => await InstallNpmPackageAsync(packageName, version),
                    _ => false
                };

                if (success)
                {
                    package.IsInstalled = true;

                    var key = $"{language}:{packageName}";
                    if (!_installedPackages.ContainsKey(key))
                    {
                        _installedPackages[key] = new List<PackageInfo>();
                    }
                    _installedPackages[key].Add(package);

                    _logger.LogInformation("Installed {Language} package {PackageName} version {Version}",
                        language, packageName, version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install package {PackageName}", packageName);
                package.SecurityIssue = ex.Message;
            }

            return package;
        }

        private bool IsPackageSafe(string packageName)
        {
            // Simplified safety check - in production would check against vulnerability databases
            var unsafePatterns = new[] { "exec", "eval", "shell", "hack" };
            return !unsafePatterns.Any(p => packageName.ToLower().Contains(p));
        }

        private async Task<bool> InstallPythonPackageAsync(string packageName, string? version)
        {
            try
            {
                var versionArg = version != null ? $"=={version}" : "";
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pip",
                    Arguments = $"install {packageName}{versionArg}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install Python package {PackageName}", packageName);
            }

            return false;
        }

        private async Task<bool> InstallNpmPackageAsync(string packageName, string? version)
        {
            try
            {
                var versionArg = version != null ? $"@{version}" : "";
                var startInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = $"install {packageName}{versionArg}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _workDir
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install npm package {PackageName}", packageName);
            }

            return false;
        }

        public List<PackageInfo> GetInstalledPackages(string language)
        {
            var packages = new List<PackageInfo>();
            foreach (var key in _installedPackages.Keys.Where(k => k.StartsWith($"{language}:")))
            {
                if (_installedPackages.TryGetValue(key, out var pkgs))
                {
                    packages.AddRange(pkgs);
                }
            }
            return packages;
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalExecutions"] = _executions.Count,
                ["completedExecutions"] = _executions.Values.Count(e => e.Status == ExecutionStatus.Completed),
                ["failedExecutions"] = _executions.Values.Count(e => e.Status == ExecutionStatus.Failed),
                ["activeDebugSessions"] = _debugSessions.Values.Count(s => s.State != DebugState.Completed),
                ["installedPackages"] = _installedPackages.Values.Sum(p => p.Count)
            };
        }

        #endregion
    }
}

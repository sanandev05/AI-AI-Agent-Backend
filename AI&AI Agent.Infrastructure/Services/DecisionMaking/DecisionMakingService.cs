using AI_AI_Agent.Domain.DecisionMaking;

namespace AI_AI_Agent.Infrastructure.Services.DecisionMaking
{
    /// <summary>
    /// Decision making framework with confidence scoring and approval gates
    /// </summary>
    public class DecisionMakingService
    {
        private readonly Dictionary<string, Decision> _decisions = new();
        private readonly Dictionary<string, Func<Decision, Task<bool>>> _approvalHandlers = new();

        /// <summary>
        /// Create a new decision point
        /// </summary>
        public Decision CreateDecision(string context, bool requiresApproval = false)
        {
            var decision = new Decision
            {
                Context = context,
                RequiresApproval = requiresApproval
            };

            _decisions[decision.Id] = decision;
            return decision;
        }

        /// <summary>
        /// Add an option to a decision
        /// </summary>
        public void AddOption(
            Decision decision,
            string description,
            double confidence,
            Dictionary<string, double>? criteria = null,
            string reasoning = "",
            List<string>? pros = null,
            List<string>? cons = null)
        {
            var option = new DecisionOption
            {
                Description = description,
                Confidence = confidence,
                Criteria = criteria ?? new Dictionary<string, double>(),
                Reasoning = reasoning,
                Pros = pros ?? new List<string>(),
                Cons = cons ?? new List<string>()
            };

            decision.Options.Add(option);
        }

        /// <summary>
        /// Select the best option based on strategy
        /// </summary>
        public async Task<DecisionOption> MakeDecisionAsync(
            Decision decision,
            Dictionary<string, double>? criteriaWeights = null,
            CancellationToken cancellationToken = default)
        {
            if (decision.Options.Count == 0)
            {
                throw new InvalidOperationException("No options available for decision");
            }

            DecisionOption selected;

            switch (decision.Strategy)
            {
                case DecisionStrategy.HighestConfidence:
                    selected = decision.Options.OrderByDescending(o => o.Confidence).First();
                    break;

                case DecisionStrategy.WeightedCriteria:
                    selected = SelectByWeightedCriteria(decision.Options, criteriaWeights ?? new());
                    break;

                case DecisionStrategy.MinimizeRisk:
                    selected = decision.Options.OrderBy(o => o.Cons.Count)
                                               .ThenByDescending(o => o.Confidence)
                                               .First();
                    break;

                case DecisionStrategy.MaximizeReward:
                    selected = decision.Options.OrderBy(o => o.Pros.Count)
                                               .ThenByDescending(o => o.Confidence)
                                               .First();
                    break;

                case DecisionStrategy.Balanced:
                    selected = decision.Options.OrderByDescending(o =>
                        (o.Confidence * 0.4) +
                        (o.Pros.Count * 0.3) -
                        (o.Cons.Count * 0.3)
                    ).First();
                    break;

                case DecisionStrategy.UserChoice:
                    // Wait for user selection (would be handled by approval mechanism)
                    throw new InvalidOperationException("UserChoice strategy requires manual selection");

                default:
                    selected = decision.Options.OrderByDescending(o => o.Confidence).First();
                    break;
            }

            decision.SelectedOption = selected;
            decision.DecidedAt = DateTime.UtcNow;

            // Handle approval if required
            if (decision.RequiresApproval)
            {
                var approved = await RequestApprovalAsync(decision, cancellationToken);
                if (!approved)
                {
                    // Try fallback option
                    if (!string.IsNullOrEmpty(selected.FallbackOptionId))
                    {
                        var fallback = decision.Options.FirstOrDefault(o => o.Id == selected.FallbackOptionId);
                        if (fallback != null)
                        {
                            decision.SelectedOption = fallback;
                            decision.ApprovalReason = "Primary option rejected, using fallback";
                        }
                    }
                }
            }

            return decision.SelectedOption;
        }

        /// <summary>
        /// Select option using weighted criteria
        /// </summary>
        private DecisionOption SelectByWeightedCriteria(
            List<DecisionOption> options,
            Dictionary<string, double> weights)
        {
            var scores = options.Select(option =>
            {
                var score = 0.0;
                foreach (var criterion in option.Criteria)
                {
                    if (weights.TryGetValue(criterion.Key, out var weight))
                    {
                        score += criterion.Value * weight;
                    }
                }
                return new { Option = option, Score = score };
            }).OrderByDescending(x => x.Score);

            return scores.First().Option;
        }

        /// <summary>
        /// Request approval for a decision
        /// </summary>
        private async Task<bool> RequestApprovalAsync(Decision decision, CancellationToken cancellationToken)
        {
            // Check if there's a registered approval handler
            if (_approvalHandlers.TryGetValue(decision.Context, out var handler))
            {
                decision.IsApproved = await handler(decision);
                return decision.IsApproved;
            }

            // Default: auto-approve if confidence is high
            if (decision.SelectedOption != null && decision.SelectedOption.Confidence >= 0.8)
            {
                decision.IsApproved = true;
                decision.ApprovalReason = "Auto-approved (high confidence)";
                return true;
            }

            // Otherwise require manual approval (would integrate with approval service)
            decision.IsApproved = false;
            decision.ApprovalReason = "Requires manual approval";
            return false;
        }

        /// <summary>
        /// Register an approval handler for specific decision contexts
        /// </summary>
        public void RegisterApprovalHandler(string context, Func<Decision, Task<bool>> handler)
        {
            _approvalHandlers[context] = handler;
        }

        /// <summary>
        /// Build a decision tree for complex decisions
        /// </summary>
        public DecisionTree BuildDecisionTree(string rootContext, List<string> possiblePaths)
        {
            return new DecisionTree
            {
                RootDecision = CreateDecision(rootContext),
                Paths = possiblePaths.Select(path => new DecisionPath
                {
                    Description = path,
                    Decisions = new List<Decision>()
                }).ToList()
            };
        }

        /// <summary>
        /// Get decision by ID
        /// </summary>
        public Decision? GetDecision(string decisionId)
        {
            _decisions.TryGetValue(decisionId, out var decision);
            return decision;
        }

        /// <summary>
        /// Get decision statistics
        /// </summary>
        public Dictionary<string, object> GetDecisionStats(Decision decision)
        {
            return new Dictionary<string, object>
            {
                { "OptionCount", decision.Options.Count },
                { "AverageConfidence", decision.Options.Count > 0 ? decision.Options.Average(o => o.Confidence) : 0 },
                { "MaxConfidence", decision.Options.Count > 0 ? decision.Options.Max(o => o.Confidence) : 0 },
                { "MinConfidence", decision.Options.Count > 0 ? decision.Options.Min(o => o.Confidence) : 0 },
                { "RequiresApproval", decision.RequiresApproval },
                { "IsApproved", decision.IsApproved },
                { "HasFallback", decision.SelectedOption?.FallbackOptionId != null }
            };
        }

        /// <summary>
        /// Export decision analysis
        /// </summary>
        public string ExportDecisionAnalysis(Decision decision)
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine($"Decision Analysis: {decision.Id}");
            output.AppendLine($"Context: {decision.Context}");
            output.AppendLine($"Strategy: {decision.Strategy}");
            output.AppendLine($"Requires Approval: {decision.RequiresApproval}");
            output.AppendLine();

            output.AppendLine($"Options ({decision.Options.Count}):");
            foreach (var option in decision.Options.OrderByDescending(o => o.Confidence))
            {
                output.AppendLine($"\n  Option: {option.Description}");
                output.AppendLine($"  Confidence: {option.Confidence:F2}");
                output.AppendLine($"  Reasoning: {option.Reasoning}");
                
                if (option.Pros.Count > 0)
                {
                    output.AppendLine($"  Pros: {string.Join(", ", option.Pros)}");
                }
                
                if (option.Cons.Count > 0)
                {
                    output.AppendLine($"  Cons: {string.Join(", ", option.Cons)}");
                }

                if (decision.SelectedOption?.Id == option.Id)
                {
                    output.AppendLine("  ★ SELECTED");
                }
            }

            if (decision.SelectedOption != null)
            {
                output.AppendLine($"\nSelected: {decision.SelectedOption.Description}");
                output.AppendLine($"Decided At: {decision.DecidedAt}");
            }

            return output.ToString();
        }
    }

    /// <summary>
    /// Represents a decision tree for complex multi-step decisions
    /// </summary>
    public class DecisionTree
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Decision RootDecision { get; set; } = null!;
        public List<DecisionPath> Paths { get; set; } = new();
    }

    /// <summary>
    /// Represents a path through the decision tree
    /// </summary>
    public class DecisionPath
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = string.Empty;
        public List<Decision> Decisions { get; set; } = new();
        public double PathProbability { get; set; } = 1.0;
    }
}

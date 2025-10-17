using AI_AI_Agent.Domain.Reasoning;

namespace AI_AI_Agent.Infrastructure.Services.Reasoning
{
    /// <summary>
    /// Multi-step reasoning engine with chain-of-thought and self-correction
    /// </summary>
    public class ReasoningEngine
    {
        private readonly Dictionary<string, ReasoningTrace> _traces = new();

        /// <summary>
        /// Start a new reasoning trace
        /// </summary>
        public ReasoningTrace StartReasoning(string goal)
        {
            var trace = new ReasoningTrace
            {
                Goal = goal
            };

            _traces[trace.Id] = trace;
            return trace;
        }

        /// <summary>
        /// Add a reasoning step using chain-of-thought
        /// </summary>
        public ReasoningStep AddReasoningStep(
            ReasoningTrace trace,
            string thought,
            string action,
            string observation,
            double confidence = 1.0)
        {
            var step = new ReasoningStep
            {
                StepNumber = trace.Steps.Count + 1,
                Thought = thought,
                Action = action,
                Observation = observation,
                Confidence = confidence
            };

            trace.Steps.Add(step);
            return step;
        }

        /// <summary>
        /// Generate chain-of-thought prompt for LLM
        /// </summary>
        public string GenerateChainOfThoughtPrompt(string goal, string context = "")
        {
            return $@"
Goal: {goal}

Context: {context}

Please think through this step-by-step using the following format:

Thought: [Your reasoning about what to do next]
Action: [The specific action you will take]
Observation: [What you expect to observe from this action]

Continue this process until you reach the goal. Be explicit about your reasoning at each step.

After completing all steps, provide your final conclusion.
";
        }

        /// <summary>
        /// Parse chain-of-thought response from LLM
        /// </summary>
        public void ParseChainOfThoughtResponse(ReasoningTrace trace, string response)
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? currentThought = null;
            string? currentAction = null;
            string? currentObservation = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("Thought:", StringComparison.OrdinalIgnoreCase))
                {
                    // Save previous step if exists
                    if (currentThought != null && currentAction != null && currentObservation != null)
                    {
                        AddReasoningStep(trace, currentThought, currentAction, currentObservation);
                    }

                    currentThought = trimmedLine.Substring("Thought:".Length).Trim();
                    currentAction = null;
                    currentObservation = null;
                }
                else if (trimmedLine.StartsWith("Action:", StringComparison.OrdinalIgnoreCase))
                {
                    currentAction = trimmedLine.Substring("Action:".Length).Trim();
                }
                else if (trimmedLine.StartsWith("Observation:", StringComparison.OrdinalIgnoreCase))
                {
                    currentObservation = trimmedLine.Substring("Observation:".Length).Trim();
                }
                else if (trimmedLine.StartsWith("Conclusion:", StringComparison.OrdinalIgnoreCase))
                {
                    trace.Conclusion = trimmedLine.Substring("Conclusion:".Length).Trim();
                }
            }

            // Save last step if exists
            if (currentThought != null && currentAction != null && currentObservation != null)
            {
                AddReasoningStep(trace, currentThought, currentAction, currentObservation);
            }
        }

        /// <summary>
        /// Verify reasoning steps for logical consistency
        /// </summary>
        public bool VerifyReasoning(ReasoningTrace trace)
        {
            trace.VerificationChecks.Clear();
            bool isValid = true;

            // Check 1: At least one reasoning step
            if (trace.Steps.Count == 0)
            {
                trace.VerificationChecks.Add("FAIL: No reasoning steps provided");
                isValid = false;
            }
            else
            {
                trace.VerificationChecks.Add($"PASS: {trace.Steps.Count} reasoning steps provided");
            }

            // Check 2: All steps have non-empty thought, action, observation
            var incompleteSteps = trace.Steps.Where(s =>
                string.IsNullOrWhiteSpace(s.Thought) ||
                string.IsNullOrWhiteSpace(s.Action) ||
                string.IsNullOrWhiteSpace(s.Observation)).ToList();

            if (incompleteSteps.Any())
            {
                trace.VerificationChecks.Add($"FAIL: {incompleteSteps.Count} incomplete steps");
                isValid = false;
            }
            else
            {
                trace.VerificationChecks.Add("PASS: All steps are complete");
            }

            // Check 3: Confidence scores are reasonable
            var lowConfidenceSteps = trace.Steps.Where(s => s.Confidence < 0.3).ToList();
            if (lowConfidenceSteps.Any())
            {
                trace.VerificationChecks.Add($"WARNING: {lowConfidenceSteps.Count} steps with low confidence (<0.3)");
            }
            else
            {
                trace.VerificationChecks.Add("PASS: All steps have acceptable confidence");
            }

            // Check 4: Conclusion is provided
            if (string.IsNullOrWhiteSpace(trace.Conclusion))
            {
                trace.VerificationChecks.Add("FAIL: No conclusion provided");
                isValid = false;
            }
            else
            {
                trace.VerificationChecks.Add("PASS: Conclusion provided");
            }

            trace.IsVerified = isValid;
            return isValid;
        }

        /// <summary>
        /// Self-correction: identify and fix reasoning errors
        /// </summary>
        public string GenerateSelfCorrectionPrompt(ReasoningTrace trace)
        {
            var stepsText = string.Join("\n\n", trace.Steps.Select(s =>
                $"Step {s.StepNumber}:\nThought: {s.Thought}\nAction: {s.Action}\nObservation: {s.Observation}"));

            return $@"
Review the following reasoning trace for potential errors or improvements:

Goal: {trace.Goal}

Reasoning Steps:
{stepsText}

Conclusion: {trace.Conclusion}

Please analyze this reasoning and:
1. Identify any logical flaws or inconsistencies
2. Suggest corrections or improvements
3. Verify that the conclusion follows from the steps

Provide your analysis in this format:
Issues Found: [list any problems]
Corrections: [suggested fixes]
Revised Conclusion: [if needed]
";
        }

        /// <summary>
        /// Apply self-corrections to reasoning trace
        /// </summary>
        public void ApplySelfCorrection(ReasoningTrace trace, string correctionResponse)
        {
            // Parse correction response and update trace
            trace.Metadata["self_correction"] = correctionResponse;
            trace.Metadata["corrected_at"] = DateTime.UtcNow.ToString("o");
        }

        /// <summary>
        /// Calculate reasoning metrics
        /// </summary>
        public void CalculateMetrics(ReasoningTrace trace)
        {
            trace.Metrics["total_steps"] = trace.Steps.Count;
            trace.Metrics["average_confidence"] = trace.Steps.Count > 0
                ? trace.Steps.Average(s => s.Confidence)
                : 0.0;
            trace.Metrics["min_confidence"] = trace.Steps.Count > 0
                ? trace.Steps.Min(s => s.Confidence)
                : 0.0;
            trace.Metrics["is_verified"] = trace.IsVerified;
            trace.Metrics["verification_checks_passed"] = trace.VerificationChecks.Count(c => c.StartsWith("PASS"));
            trace.Metrics["verification_checks_failed"] = trace.VerificationChecks.Count(c => c.StartsWith("FAIL"));
        }

        /// <summary>
        /// Get a reasoning trace by ID
        /// </summary>
        public ReasoningTrace? GetTrace(string traceId)
        {
            _traces.TryGetValue(traceId, out var trace);
            return trace;
        }

        /// <summary>
        /// Export reasoning trace as text
        /// </summary>
        public string ExportTrace(ReasoningTrace trace)
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine($"Reasoning Trace: {trace.Id}");
            output.AppendLine($"Goal: {trace.Goal}");
            output.AppendLine($"Steps: {trace.Steps.Count}");
            output.AppendLine();

            foreach (var step in trace.Steps)
            {
                output.AppendLine($"--- Step {step.StepNumber} (Confidence: {step.Confidence:F2}) ---");
                output.AppendLine($"Thought: {step.Thought}");
                output.AppendLine($"Action: {step.Action}");
                output.AppendLine($"Observation: {step.Observation}");
                output.AppendLine();
            }

            output.AppendLine($"Conclusion: {trace.Conclusion}");
            output.AppendLine($"Verified: {trace.IsVerified}");

            if (trace.VerificationChecks.Count > 0)
            {
                output.AppendLine("\nVerification Checks:");
                foreach (var check in trace.VerificationChecks)
                {
                    output.AppendLine($"  {check}");
                }
            }

            return output.ToString();
        }
    }
}

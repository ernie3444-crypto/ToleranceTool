using System;
using System.Collections.Generic;
using System.Globalization;
using NCalc;

namespace ToleranceTool.Core.Expressions
{
    /// <summary>
    /// A single arithmetic expression (from the scale-type library or a tolerance
    /// term) parsed once and evaluated many times with different variable values.
    /// </summary>
    /// <remarks>
    /// Wraps NCalc so the rest of Core never sees it. The parsed tree is built on
    /// first use and reused; instances are not thread-safe, which matches the
    /// single-threaded calculation pass.
    /// </remarks>
    public sealed class ExpressionEvaluator
    {
        private readonly string _body;
        private readonly Expression _expression;

        public ExpressionEvaluator(string body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ExpressionException("The expression is empty.");
            }

            _expression = new Expression(body, EvaluateOptions.IgnoreCase);

            if (_expression.HasErrors())
            {
                throw new ExpressionException($"Cannot parse \"{body}\": {_expression.Error}");
            }
        }

        public string Body => _body;

        /// <summary>
        /// Evaluates the expression. Every variable the body references must be
        /// present in <paramref name="variables"/>.
        /// </summary>
        public double Evaluate(IReadOnlyDictionary<string, double> variables)
        {
            if (variables == null)
            {
                throw new ArgumentNullException(nameof(variables));
            }

            _expression.Parameters.Clear();
            foreach (KeyValuePair<string, double> pair in variables)
            {
                _expression.Parameters[pair.Key] = pair.Value;
            }

            object result;
            try
            {
                result = _expression.Evaluate();
            }
            catch (Exception ex)
            {
                throw new ExpressionException($"Cannot evaluate \"{_body}\": {ex.Message}", ex);
            }

            try
            {
                return Convert.ToDouble(result, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new ExpressionException(
                    $"\"{_body}\" evaluated to '{result ?? "null"}', which is not a number.", ex);
            }
        }

        /// <summary>Parses <paramref name="body"/> and returns null if it is well-formed, else the error text.</summary>
        public static string? Validate(string body)
        {
            try
            {
                _ = new ExpressionEvaluator(body);
                return null;
            }
            catch (ExpressionException ex)
            {
                return ex.Message;
            }
        }
    }

    /// <summary>Thrown when an expression cannot be parsed or evaluated.</summary>
    public sealed class ExpressionException : Exception
    {
        public ExpressionException(string message)
            : base(message)
        {
        }

        public ExpressionException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}

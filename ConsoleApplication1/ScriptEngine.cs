//#define DUMP_TOKENS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Trio.SharedLibrary
{
    /// <summary>
    /// Script engine
    /// </summary>
    public class ScriptEngine
    {
        #region tokens
        enum TokenType
        {
            String,
            Double,
            Integer,
            HexNumber,
            Boolean,
            Identifier,
            Keyword,
            Symbol
        }

        class Token
        {
            public TokenType Type;
            public int Line;
            public int Column;
            public string ScriptName;
            public string Value;
            public Token Next;
            public Token(TokenType type, string value)
            {
                Type = type;
                Value = value;
            }
        }

        // reg-ex of the script language
        static Regex regTokens = new Regex(@"(?'cmnt'//.*$)|" + // single line comment
                                           @"(?'ws'\s+)|" + // white space
                                           @"""(?'str'(?:\\""|[^""])*)""|" + // string literal
                                           @"(?i:0x(?'hex'[\da-f]+))|" + // hexadecimal literal
                                           @"(?'double'(?:\d*\.)?\d+[eE][-+]?\d+|\d*\.\d+)|" + // double literal
                                           @"(?'int'\d+)|" + // integer literal
                                           @"\b(?'bool'true|false)\b|" + // boolean literal
                                           @"\b(?'kwd'func|var|foreach|for|in|if|else|while|return|break|continue)\b|" + // keyword
                                           @"\b(?'id'[a-zA-Z_][a-zA-Z_\d]*)\b|" + // identifier
                                           @"(?'sym'\.\.\.|[<>!=]=|&&|\|\||<<|>>|[-+~!/*%&^|?:=(){}[\];,<>])|" + // symbol (-+*/%&^| - these symbols are not yet supported as assignments)
                                           @"(?'other'.)", RegexOptions.Compiled); // anything else -  not recognized

        static Token Tokenize(string text, string scriptName)
        {
            if (text == null)
                return null;

            // unify line endings
            text = text.Replace("\r\n", "\n");
            // split by new line
            var lines = text.Split('\n');
            // tokenize by lines
            return Tokenize(lines, scriptName);
        }

        static Token Tokenize(string[] lines, string scriptName)
        {
            if (lines == null || lines.Length == 0)
                return null;

            Token first = null;
            Token last = null;
            for (int line = 0; line < lines.Length; line++)
            {
                string text = lines[line];
                var matches = regTokens.Matches(text);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        Token token = null;
                        var groups = match.Groups;
                        if (groups["cmnt"].Success)
                        {
#if DUMP_TOKENS
                            Console.WriteLine("{0}({1},{2},Comment) : {3}", scriptName, line + 1, match.Index + 1, match.Value);
#endif
                            continue;
                        }
                        if (groups["ws"].Success)
                        {
#if DUMP_TOKENS
                            Console.WriteLine("{0}({1},{2},WhiteSpace)", scriptName, line + 1, match.Index + 1);
#endif
                            continue;
                        }
                        if (groups["str"].Success)
                            token = new Token(TokenType.String, EscapeSpecialChars(groups["str"].Value));
                        else if (groups["double"].Success)
                            token = new Token(TokenType.Double, match.Value);
                        else if (groups["int"].Success)
                            token = new Token(TokenType.Integer, match.Value);
                        else if (groups["hex"].Success)
                            token = new Token(TokenType.HexNumber, groups["hex"].Value);
                        else if (groups["bool"].Success)
                            token = new Token(TokenType.Boolean, match.Value);
                        else if (groups["kwd"].Success)
                            token = new Token(TokenType.Keyword, match.Value);
                        else if (groups["id"].Success)
                            token = new Token(TokenType.Identifier, match.Value);
                        else if (groups["sym"].Success)
                            token = new Token(TokenType.Symbol, match.Value);
                        else
                            throw new ApplicationException(string.Format("{0}({1},{2}) - unknown token {3}", scriptName, line + 1, match.Index + 1, match.Value));

                        token.Line = line;
                        token.Column = match.Index;
                        token.ScriptName = scriptName;
#if DUMP_TOKENS
                        Console.WriteLine("{0}({1},{2},{3}): {4}", token.ScriptName, token.Line + 1, token.Column + 1, token.Type, match.Value);
#endif
                        if (last != null)
                            last.Next = token;
                        last = token;
                        if (first == null)
                            first = token;
                    }
                }
            }
            return first;
        }

        static Regex _regStringEscape = new Regex(@"\\(?:(?'oct'[0-6]{1,3})|x(?'hex'(?-i:[\da-f]{1,4}))|(?'char'.))", RegexOptions.Compiled);

        static string EscapeSpecialChars(string input)
        {
            return input == null ? null :
                    _regStringEscape.Replace(input, m =>
                        {
                            var groups = m.Groups;
                            if (groups["oct"].Success)
                            {
                                short oct = Convert.ToInt16(groups["oct"].Value, 8);
                                return Convert.ToChar(oct).ToString();
                            }
                            else if (groups["hex"].Success)
                            {
                                short hex = Convert.ToInt16(groups["hex"].Value, 16);
                                return Convert.ToChar(hex).ToString();
                            }
                            else
                            {
                                Debug.Assert(groups["char"].Success);
                                var value = groups["char"].Value;
                                switch (value)
                                {
                                    case "a":
                                        return "\a";
                                    case "b":
                                        return "\b";
                                    case "t":
                                        return "\t";
                                    case "v":
                                        return "\v";
                                    case "n":
                                        return "\n";
                                    case "r":
                                        return "\r";
                                    case "f":
                                        return "\f";
                                    default:
                                        return value;
                                }
                            }
                        });
        }
        #endregion tokens

        #region errors
        /// <summary>
        /// Exception thrown during parsing phase
        /// </summary>
        public class ParserException : ApplicationException
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public ParserException(string message) : base(message) { }
        }
        static ParserException ParserExceptionWithToken(Token token, string message)
        {
            return new ParserException(token == null ? message : string.Format("{0}({1},{2}): {3}", token.ScriptName, token.Line + 1, token.Column + 1, message));
        }
        /// <summary>
        /// Exception thrown during execution phase
        /// </summary>
        public class ExecutionException : ApplicationException
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public ExecutionException(string message) : base(message) { }
        }
        static ExecutionException ExecutionExceptionWithToken(Token token, string message)
        {
            return new ExecutionException(token == null ? message : string.Format("{0}({1},{2}): {3}", token.ScriptName, token.Line + 1, token.Column + 1, message));
        }

        class ReturnException : ApplicationException
        {
            public Token Token { get; private set; }
            public Value Value { get; private set; }

            public ReturnException(Token token, Value value)
            {
                Token = token;
                Value = value;
            }
        }

        class BreakOrContinueException : ApplicationException
        {
            public Token Token { get; private set; }
            public bool Break { get; private set; }
            public BreakOrContinueException(Token token, bool @break)
            {
                Token = token;
                Break = @break;
            }
        }

        const string _errUnexpectedEndOfFile = "unexpected end of file";
        const string _errIdentifierExpected = "identifier expected";
        const string _errStatementExpected = "statement expected";
        const string _errExpressionExpected = "expression expected";
        const string _errAssignmentOperatorExpected = "assignment operator expected";
        const string _errSubExpressionExpected = "sub-expression expected";
        const string _errfmt_0_Expected = "{0} expected";
        const string _errfmtVariable_0_AlreadyDefined = "variable {0} already defined";
        const string _errfmtFunction_0_AlreadyDefined = "function {0} already defined";
        const string _errfmtVariable_0_NotDefined = "variable {0} not defined";
        const string _errfmtVariable_0_NotInitialized = "variable {0} not initialized";
        const string _errfmtVariable_0_NotCollection = "variable {0} not collection";
        const string _errExpressionNotCollection = "expression is not collection";
        const string _errfmtFailedToApply_0_operator_1 = "failed to apply {0} operator - {1}";
        const string _errIndexExpressionIsNotInteger = "index expression is not an integer";
        const string _errfmtIndexOutOfRange = "index {0} out of range";
        const string _errBooleanValueExpected = "boolean value expected";
        const string _errfmtFunction_0_NotDefined = "function {0} not defined";
        const string _errParametersCountMismatch = "parameters count mismatch";
        const string _errFunctionShouldReturnValue = "function should return value";
        const string _errExpressionIsNotBoolean = "expression is not boolean";
        const string _errLeftOperandIsNotBoolean = "left operand is not boolean";
        const string _errRightOperandIsNotBoolean = "right operand is not boolean";
        const string _errInnerValueIsNotBoolean = "inner value is not boolean";
        const string _errInnerValueIsNotNumber = "inner value is not number";
        const string _errInnerValueIsNotInteger = "inner value is not integer";
        const string _errUnableToConvertToIngeger = "unable to convert value to integer";
        const string _errDefinedForCombinationOfInputValues = "not defined for combination of input values";
        const string _errEllipsisShouldBeLast = "ellipsis should be at the end in the parameter list";
        const string _errBreakWithoutSurroundingForOrWhile = "break without surrounding for or while";
        const string _errContinueWithoutSurroundingForOrWhile = "continue without surrounding for or while";
        const string _errfmtUnexpectedToken_0_ = "unexpected token {0}";
        #endregion errors

        #region expressions

        abstract class Expr : Stmt
        {
            public Expr(Token startToken) : base(startToken) { }

            public override void Execute(Context context)
            {
                Calculate(context);
            }
            public abstract Value Calculate(Context context);

            public bool CheckCondition(Context context)
            {
                var cond = Calculate(context) as ValueBool;
                if (cond == null)
                    throw ExecutionExceptionWithToken(StartToken, _errExpressionIsNotBoolean);

                return cond.V;
            }
        }

        #region unary and binary operators


        class BaseOp
        {
            public Token StartToken { get; set; }
            public string OpName { get; private set; }
            public BaseOp(string opName) { OpName = opName; }

            protected Value CheckValueNotNull(Value value, string errorMessage)
            {
                if (value == null)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtFailedToApply_0_operator_1, OpName, errorMessage));
                return value;
            }
        }

        abstract class BinOp : BaseOp
        {
            public BinOp(string opName) : base(opName) { }

            public abstract Value Apply(Context context, Expr Left, Expr Right);

            public BinOp Clone() { return (BinOp)MemberwiseClone(); }
        }

        class BinOpDelegates : BinOp
        {
            public Func<ValueDouble, ValueDouble, Value> OpDouble { get; private set; }
            public Func<ValueInt, ValueInt, Value> OpInt { get; private set; }
            public Func<bool, bool, bool> OpBool { get; private set; }
            public Func<ValueString, ValueString, Value> OpString { get; private set; }
            public Func<ValueList, ValueList, Value> OpList { get; private set; }

            public BinOpDelegates(string opName,
                         Func<ValueDouble, ValueDouble, Value> opDouble,
                         Func<ValueInt, ValueInt, Value> opInt,
                         Func<bool, bool, bool> opBool = null,
                         Func<ValueString, ValueString, Value> opString = null,
                         Func<ValueList, ValueList, Value> opList = null)
                : base(opName)
            {
                OpDouble = opDouble;
                OpInt = opInt;
                OpBool = opBool;
                OpString = opString;
                OpList = opList;
            }

            public override Value Apply(Context context, Expr Left, Expr Right)
            {
                return ValueHelper.ApplyBinOp(StartToken, this, Left.Calculate(context), Right.Calculate(context));
            }
        }

        class BinOpLogAnd : BinOp
        {
            public BinOpLogAnd() : base("&&") { }

            public override Value Apply(Context context, Expr Left, Expr Right)
            {
                var left = Left.Calculate(context) as ValueBool;
                CheckValueNotNull(left, _errLeftOperandIsNotBoolean);
                if (left.V)
                {
                    var right = Right.Calculate(context) as ValueBool;
                    CheckValueNotNull(right, _errRightOperandIsNotBoolean);
                    return right;
                }
                else
                {
                    return ValueBool.False;
                }
            }
        }

        class BinOpLogOr : BinOp
        {
            public BinOpLogOr() : base("||") { }
            public override Value Apply(Context context, Expr Left, Expr Right)
            {
                var left = Left.Calculate(context) as ValueBool;
                CheckValueNotNull(left, _errLeftOperandIsNotBoolean);
                if (left.V)
                {
                    return ValueBool.True;
                }
                else
                {
                    var right = Right.Calculate(context) as ValueBool;
                    CheckValueNotNull(right, _errRightOperandIsNotBoolean);
                    return right;
                }
            }
        }

        abstract class UnOp : BaseOp
        {
            public UnOp(string opName) : base(opName) { }

            public UnOp Clone() { return (UnOp)MemberwiseClone(); }

            public abstract Value Apply(Value innerValue);
        }

        class UnOpLogNeg : UnOp
        {
            public UnOpLogNeg() : base("!") { }
            public override Value Apply(Value innerValue)
            {
                var valueBool = innerValue as ValueBool;
                if (valueBool != null)
                    return ValueBool.Box(!valueBool.V);
                return CheckValueNotNull(null, _errInnerValueIsNotBoolean);
            }
        }

        class UnOpNeg : UnOp
        {
            public UnOpNeg() : base("-") { }
            public override Value Apply(Value innerValue)
            {
                var valueInt = innerValue as ValueInt;
                if (valueInt != null)
                    return new ValueInt(-valueInt.V);
                var valueDouble = innerValue as ValueDouble;
                if (valueDouble != null)
                    return new ValueDouble(-valueDouble.V);
                return CheckValueNotNull(null, _errInnerValueIsNotNumber);
            }
        }

        class UnOpCompl : UnOp
        {
            public UnOpCompl() : base("~") { }
            public override Value Apply(Value innerValue)
            {
                var valueInt = innerValue as ValueInt;
                if (valueInt != null)
                    return new ValueInt(~valueInt.V);
                return CheckValueNotNull(null, _errInnerValueIsNotInteger);
            }
        }

        abstract class OpHelper
        {
            public abstract Expr Match(ref Token token);
        }

        class UnaryOpHelper : OpHelper
        {
            public UnOp[] Ops { get; private set; }

            public UnaryOpHelper(params UnOp[] ops)
            {
                Debug.Assert(ops.Length > 0);
                Ops = ops;
            }

            public override Expr Match(ref Token token)
            {
                var startToken = token;
                foreach (var op in Ops)
                {
                    if (MatchSymbol(ref token, op.OpName, false))
                    {
                        var expr = Match(ref token);
                        if (expr == null)
                            throw ParserExceptionWithToken(token, _errSubExpressionExpected);

                        var opCopy = op.Clone();
                        opCopy.StartToken = startToken;
                        return new Expr_UnOp(startToken, op, expr);
                    }
                }
                return MatchPrimExpr(ref token);
            }
        }

        class BinOpHelper : OpHelper
        {
            public OpHelper Next { get; private set; }
            public BinOp[] Ops { get; private set; }

            public BinOpHelper(OpHelper next, params BinOp[] ops)
            {
                Debug.Assert(next != null);
                Next = next;
                Debug.Assert(ops.Length > 0);
                Ops = ops;
            }

            public override Expr Match(ref Token token)
            {
                var startToken = token;
                var left = Next.Match(ref token);
                if (left == null)
                    return null;

                bool opFound;
                do
                {
                    opFound = false;
                    foreach (var op in Ops)
                    {
                        var symbolToken = token;
                        if (MatchSymbol(ref token, op.OpName, false))
                        {
                            var right = Next.Match(ref token);
                            if (right == null)
                                throw ParserExceptionWithToken(token, _errSubExpressionExpected);

                            var opCopy = op.Clone();
                            opCopy.StartToken = symbolToken;
                            left = new Expr_BinOp(startToken, opCopy, left, right);
                            opFound = true;
                            break;
                        }
                    }
                }
                while (opFound);

                return left;
            }
        }

        static UnaryOpHelper _unaryOp = new UnaryOpHelper(new UnOpNeg(), new UnOpLogNeg(), new UnOpCompl());
        static BinOpHelper _binaryMulOpHelper = new BinOpHelper(_unaryOp,
            new BinOpDelegates("*", (ValueDouble l, ValueDouble r) => new ValueDouble(l.V * r.V), (ValueInt l, ValueInt r) => new ValueInt(l.V * r.V)),
            new BinOpDelegates("/", (ValueDouble l, ValueDouble r) => new ValueDouble(l.V / r.V), (ValueInt l, ValueInt r) => new ValueInt(l.V / r.V)),
            new BinOpDelegates("%", (ValueDouble l, ValueDouble r) => new ValueDouble(l.V % r.V), (ValueInt l, ValueInt r) => new ValueInt(l.V % r.V)));
        static BinOpHelper _binaryAddOpHelper = new BinOpHelper(_binaryMulOpHelper,
            new BinOpDelegates("+", (ValueDouble l, ValueDouble r) => new ValueDouble(l.V + r.V), (ValueInt l, ValueInt r) => new ValueInt(l.V + r.V), null, (ValueString l, ValueString r) => new ValueString(l.V + r.V), (ValueList l, ValueList r) => new ValueList(new List<Value>(l.V.Concat(r.V)))),
            new BinOpDelegates("-", (ValueDouble l, ValueDouble r) => new ValueDouble(l.V - r.V), (ValueInt l, ValueInt r) => new ValueInt(l.V - r.V)));
        static BinOpHelper _binaryShiftOpHelper = new BinOpHelper(_binaryAddOpHelper,
            new BinOpDelegates("<<", null, (ValueInt l, ValueInt r) => new ValueInt(l.V << r.V)),
            new BinOpDelegates(">>", null, (ValueInt l, ValueInt r) => new ValueInt(l.V >> r.V)));
        static BinOpHelper _binaryRelOpHelper = new BinOpHelper(_binaryShiftOpHelper,
            new BinOpDelegates("<", (ValueDouble l, ValueDouble r) => new ValueBool(l.V < r.V), (ValueInt l, ValueInt r) => new ValueBool(l.V < r.V)),
            new BinOpDelegates(">", (ValueDouble l, ValueDouble r) => new ValueBool(l.V > r.V), (ValueInt l, ValueInt r) => new ValueBool(l.V > r.V)),
            new BinOpDelegates("<=", (ValueDouble l, ValueDouble r) => new ValueBool(l.V <= r.V), (ValueInt l, ValueInt r) => new ValueBool(l.V <= r.V)),
            new BinOpDelegates(">=", (ValueDouble l, ValueDouble r) => new ValueBool(l.V >= r.V), (ValueInt l, ValueInt r) => new ValueBool(l.V >= r.V)));
        static BinOpHelper _binaryEqOpHelper = new BinOpHelper(_binaryRelOpHelper,
            new BinOpDelegates("==", (ValueDouble l, ValueDouble r) => new ValueBool(l.V == r.V), (ValueInt l, ValueInt r) => new ValueBool(l.V == r.V), (bool l, bool r) => l == r, (ValueString l, ValueString r) => new ValueBool(l.V == r.V), (ValueList l, ValueList r) => new ValueBool(l.Equals(r))),
            new BinOpDelegates("!=", (ValueDouble l, ValueDouble r) => new ValueBool(l.V != r.V), (ValueInt l, ValueInt r) => new ValueBool(l.V != r.V), (bool l, bool r) => l != r, (ValueString l, ValueString r) => new ValueBool(l.V != r.V), (ValueList l, ValueList r) => new ValueBool(!l.Equals(r))));
        static BinOpHelper _binaryAndOpHelper = new BinOpHelper(_binaryEqOpHelper,
            new BinOpDelegates("&", null, (ValueInt l, ValueInt r) => new ValueInt(l.V & r.V), (bool l, bool r) => l & r));
        static BinOpHelper _binaryXorOpHelper = new BinOpHelper(_binaryAndOpHelper,
            new BinOpDelegates("^", null, (ValueInt l, ValueInt r) => new ValueInt(l.V ^ r.V), (bool l, bool r) => l ^ r));
        static BinOpHelper _binaryOrOpHelper = new BinOpHelper(_binaryXorOpHelper,
            new BinOpDelegates("|", null, (ValueInt l, ValueInt r) => new ValueInt(l.V | r.V), (bool l, bool r) => l | r));
        static BinOpHelper _binaryLogAndOpHelper = new BinOpHelper(_binaryOrOpHelper, new BinOpLogAnd());
        static BinOpHelper _binaryLogOrOpHelper = new BinOpHelper(_binaryOrOpHelper, new BinOpLogOr());

        class Expr_UnOp : Expr
        {
            public UnOp Oper { get; private set; }
            public Expr Expr { get; private set; }
            public Expr_UnOp(Token startToken, UnOp oper, Expr expr)
                : base(startToken)
            {
                Oper = oper;
                Expr = Expr;
            }

            public override Value Calculate(Context context)
            {
                Value innerValue = Expr.Calculate(context);
                return Oper.Apply(innerValue);
            }
        }

        class Expr_BinOp : Expr
        {
            public BinOp Oper { get; private set; }
            public Expr Left { get; private set; }
            public Expr Right { get; private set; }
            public Expr_BinOp(Token startToken, BinOp oper, Expr left, Expr right)
                : base(startToken)
            {
                Oper = oper;
                Left = left;
                Right = right;
            }

            public override Value Calculate(Context context)
            {
                return Oper.Apply(context, Left, Right);
            }
        }
        #endregion unary and binary operators

        abstract class LExpr : Expr
        {
            public LExpr(Token startToken) : base(startToken) { }

            public abstract Value Assign(Context context, Value newValue);

            public static LExpr Match(ref Token token)
            {
                var startToken = token;
                var varName = MatchIdent(ref token, false);
                if (varName == null)
                    return null;

                if (MatchSymbol(ref token, "[", false))
                {
                    // consume index expression
                    Expr exprIndex = MatchExpr(ref token, true);

                    // check for closing ]
                    MatchSymbol(ref token, "]", true);

                    return new LExpr_Index(startToken, varName, exprIndex);
                }
                else
                {
                    return new LExpr_Ident(startToken, varName);
                }
            }
        }

        class LExpr_Ident : LExpr
        {
            public string Name { get; private set; }
            public LExpr_Ident(Token startToken, string name)
                : base(startToken)
            {
                Name = name;
            }

            Variable GetVariable(Context context)
            {
                var var = context.GetVariable(Name, true);
                if (var == null)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtVariable_0_NotDefined, Name));

                return var;
            }

            public override Value Calculate(Context context)
            {
                var var = GetVariable(context);
                var value = var.Value;
                if (value == null)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtVariable_0_NotInitialized, Name));
                return value;
            }

            public override Value Assign(Context context, Value newValue)
            {
                var var = GetVariable(context);
                return var.Value = newValue;
            }
        }

        class LExpr_Index : LExpr
        {
            public string Name { get; private set; }
            public Expr ExprIndex { get; private set; }
            public LExpr_Index(Token startToken, string name, Expr exprIndex)
                : base(startToken)
            {
                Name = name;
                ExprIndex = exprIndex;
            }

            Tuple<List<Value>, int> GetContext(Context context)
            {
                // get list var
                var var = context.GetVariable(Name, true);
                if (var == null)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtVariable_0_NotDefined, Name));
                var value = var.Value;
                if (value == null)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtVariable_0_NotInitialized, Name));
                var valueList = value as ValueList;
                if (valueList == null)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtVariable_0_NotCollection, Name));
                var list = valueList.V;

                // calculate index
                var valueIndex = ExprIndex.Calculate(context) as ValueInt;
                if (valueIndex == null)
                    throw ExecutionExceptionWithToken(StartToken, _errIndexExpressionIsNotInteger);
                int index = valueIndex.V;
                if (index < 0 || index >= list.Count)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtIndexOutOfRange, index));

                return Tuple.Create(list, index);
            }

            public override Value Calculate(Context context)
            {
                var ctxt = GetContext(context);
                return ctxt.Item1[ctxt.Item2];
            }

            public override Value Assign(Context context, Value newValue)
            {
                var ctxt = GetContext(context);
                return ctxt.Item1[ctxt.Item2] = newValue;
            }
        }

        class Expr_Const : Expr
        {
            public Value Value { get; private set; }
            public Expr_Const(Token startToken, Value value)
                : base(startToken)
            {
                Value = value;
            }

            public override Value Calculate(Context context) { return Value; }
        }

        #region assignment
        class Expr_Assign : Expr
        {
            public LExpr Left { get; private set; }
            public Expr Right { get; private set; }

            public Expr_Assign(Token startToken, LExpr left, Expr right)
                : base(startToken)
            {
                Left = left;
                Right = right;
            }

            public static Expr_Assign Match(ref Token token)
            {
                var startToken = token;
                var token_copy = token;
                var left = LExpr.Match(ref token_copy);
                if (left == null)
                    return null;

                // check for equality
                if (!MatchSymbol(ref token_copy, "=", false))
                    return null;
                token = token_copy;

                // expression expected
                var right = MatchExpr(ref token, true);

                return new Expr_Assign(startToken, left, right);
            }

            public override Value Calculate(Context context)
            {
                // calculate right side of the assignment
                var value = Right.Calculate(context);

                // execute assignment
                return Left.Assign(context, value);
            }
        }
        #endregion assignment

        #region condition
        class Expr_Cond : Expr
        {
            public Expr Cond { get; private set; }
            public Expr ExprTrue { get; private set; }
            public Expr ExprFalse { get; private set; }
            public Expr_Cond(Token startToken, Expr cond, Expr exprTrue, Expr exprFalse)
                : base(startToken)
            {
                Cond = cond;
                ExprTrue = exprTrue;
                ExprFalse = exprFalse;
            }
            public static Expr Match(ref Token token)
            {
                var startToken = token;
                var cond = _binaryLogOrOpHelper.Match(ref token);
                if (cond == null)
                    return null;

                if (MatchSymbol(ref token, "?", false))
                {
                    var exprTrue = Expr_Cond.Match(ref token);

                    MatchSymbol(ref token, ":", true);

                    var exprFalse = Expr_Cond.Match(ref token);

                    return new Expr_Cond(startToken, cond, exprTrue, exprFalse);
                }
                else
                {
                    return cond;
                }
            }
            public override Value Calculate(Context context)
            {
                if (Cond.CheckCondition(context))
                {
                    return ExprTrue.Calculate(context);
                }
                else
                {
                    return ExprFalse.Calculate(context);
                }
            }
        }
        #endregion condition

        #region list
        class Expr_List : Expr
        {
            public IEnumerable<Expr> ExprValues { get; private set; }

            public Expr_List(Token startToken, IEnumerable<Expr> exprValues)
                : base(startToken)
            {
                ExprValues = exprValues;
            }

            public static Expr_List Match(ref Token token)
            {
                var startToken = token;
                // check for [
                if (!MatchSymbol(ref token, "[", false))
                    return null;

                var exprValues = new List<Expr>();

                // consume expressions for list elements
                bool nextParam = false;
                for (; ; )
                {

                    // check for )
                    if (MatchSymbol(ref token, "]", false))
                        break;
                    if (nextParam)
                    {
                        // check for ,
                        MatchSymbol(ref token, ",", true);
                    }
                    nextParam = true;

                    // get element value
                    var expr = MatchExpr(ref token, true);
                    exprValues.Add(expr);
                }

                return new Expr_List(startToken, exprValues);
            }

            public override Value Calculate(Context context)
            {
                var values = new List<Value>();

                foreach (var exprValue in ExprValues)
                    values.Add(exprValue.Calculate(context));

                return new ValueList(values);
            }
        }
        #endregion list

        static Expr MatchPrimExpr(ref Token token)
        {
            var startToken = token;
            // check for func call
            var funcCall = Expr_FuncCall.Match(ref token);
            if (funcCall != null)
                return funcCall;

            // check for lvalue
            var lExpr = LExpr.Match(ref token);
            if (lExpr != null)
                return lExpr;

            // check for string literal
            if (token.Type == TokenType.String)
            {
                startToken = token;
                string sValue = token.Value;
                token = token.Next;
                return new Expr_Const(startToken, new ValueString(sValue));
            }
            // check for integer literal
            if (token.Type == TokenType.Double)
            {
                startToken = token;
                double dValue = double.Parse(token.Value, CultureInfo.InvariantCulture);
                token = token.Next;
                return new Expr_Const(startToken, new ValueDouble(dValue));
            }
            // check for int literal
            if (token.Type == TokenType.Integer)
            {
                startToken = token;
                int iValue = int.Parse(token.Value);
                token = token.Next;
                return new Expr_Const(startToken, new ValueInt(iValue));
            }
            // check for hex literal
            if (token.Type == TokenType.HexNumber)
            {
                startToken = token;
                int iValue = int.Parse(token.Value, NumberStyles.HexNumber);
                token = token.Next;
                return new Expr_Const(startToken, new ValueInt(iValue));
            }
            // check for boolean literal
            if (token.Type == TokenType.Boolean)
            {
                startToken = token;
                bool bValue = token.Value == "true";
                token = token.Next;
                return new Expr_Const(startToken, ValueBool.Box(bValue));
            }

            // check for list of values
            var list = Expr_List.Match(ref token);
            if (list != null)
                return list;

            // check for (
            if (MatchSymbol(ref token, "(", false))
            {
                // check for expression in quotes
                var expr = MatchExpr(ref token, true);

                MatchSymbol(ref token, ")", true);
                return expr;
            }

            return null;
        }

        private static Expr MatchExpr(ref Token token, bool insist)
        {
            var expr = (Expr)Expr_Assign.Match(ref token) ??
                        Expr_Cond.Match(ref token);
            if (insist && expr == null)
                throw ParserExceptionWithToken(token, _errExpressionExpected);
            return expr;
        }
        #endregion expressions

        #region value
        class ValueHelper
        {
            public static Value ApplyBinOp(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                var value = TryApplyBinOpForList(startToken, binOp, value1, value2) ??
                            TryApplyBinOpForInt(startToken, binOp, value1, value2) ??
                            TryApplyBinOpForDouble(startToken, binOp, value1, value2) ??
                            TryApplyBinOpForString(startToken, binOp, value1, value2) ??
                            TryApplyBinOpForBool(startToken, binOp, value1, value2);
                if (value == null)
                    throw ExecutionExceptionWithToken(startToken, string.Format(_errfmtFailedToApply_0_operator_1, binOp.OpName, _errDefinedForCombinationOfInputValues));
                return value;
            }

            public static Value TryApplyBinOpForBool(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpBool == null)
                    return null;
                var b1 = value1 as ValueBool;
                var b2 = value2 as ValueBool;

                return ValueBool.Box(binOp.OpBool(b1.V, b2.V));
            }

            public static Value TryApplyBinOpForInt(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpInt == null)
                    return null;

                var i1 = value1 as ValueInt;
                var i2 = value2 as ValueInt;
                if (i1 == null || i2 == null)
                    return null;

                return binOp.OpInt(i1, i2);
            }

            public static Value TryApplyBinOpForList(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpList == null)
                    return null;

                var l1 = value1 as ValueList;
                var l2 = value2 as ValueList;
                if (l1 == null || l2 == null)
                    return null;

                return binOp.OpList(l1, l2);
            }

            public static Value TryApplyBinOpForDouble(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpDouble == null)
                    return null;

                var d1 = value1 as ValueDouble;
                var d2 = value2 as ValueDouble;
                if (d1 == null)
                {
                    var i1 = value1 as ValueInt;
                    if (i1 != null)
                        d1 = new ValueDouble(i1.V);
                    return null;
                }
                if (d2 == null)
                {
                    var i2 = value2 as ValueInt;
                    if (i2 != null)
                        d2 = new ValueDouble(i2.V);
                    return null;
                }
                if (d1 == null || d2 == null)
                    return null;

                return binOp.OpDouble(d1, d2);
            }

            public static Value TryApplyBinOpForString(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpString == null)
                    return null;

                var d1 = value1 as ValueString;
                var d2 = value2 as ValueString;
                if (d1 == null || d2 == null)
                    return null;

                return binOp.OpString(d1, d2);
            }
        }

        /// <summary>
        /// Base class for script values
        /// </summary>
        public abstract class Value : IEquatable<Value>
        {
            /// <summary>
            /// Equality check for IEquatable{Value}
            /// </summary>
            /// <param name="other"></param>
            /// <returns></returns>
            public abstract bool Equals(Value other);
        }

        /// <summary>
        /// Generic base class for simple values
        /// </summary>
        /// <typeparam name="T">Actual type of the value</typeparam>
        public abstract class Value<T> : Value
        {
            /// <summary>
            /// Reference to the actual value
            /// </summary>
            public T V { get; private set; }
            /// <summary>
            /// Constructor
            /// </summary>
            public Value(T value) { V = value; }
            /// <summary>
            /// Override GetHashCode() to return value's hash
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                return V.GetHashCode();
            }
            /// <summary>
            /// Override equality check to check by value
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj))
                    return true;

                var other = obj as Value;
                if (other == null)
                    return false;

                return Equals(other);
            }
            /// <summary>
            /// Equality check for IEquatable{Value}
            /// </summary>
            public override bool Equals(Value other)
            {
                var otherT = other as Value<T>;
                return otherT != null && V.Equals(otherT.V);
            }
            /// <summary>
            /// ToString returns value
            /// </summary>
            public override string ToString()
            {
                return V.ToString();
            }
        }
        /// <summary>
        /// Class for boolean value
        /// </summary>
        public class ValueBool : Value<bool>
        {
            /// <summary>
            /// Predefined constant for true
            /// </summary>
            public readonly static ValueBool True = new ValueBool(true);
            /// <summary>
            /// Predefined constant for false
            /// </summary>
            public readonly static ValueBool False = new ValueBool(false);
            /// <summary>
            /// Boxing method
            /// </summary>
            public static ValueBool Box(bool value) { return value ? True : False; }
            /// <summary>
            /// Construcor
            /// </summary>
            /// <param name="value"></param>
            public ValueBool(bool value) : base(value) { }
        }
        /// <summary>
        /// Class for integer value
        /// </summary>
        public class ValueInt : Value<int>
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public ValueInt(int value) : base(value) { }
        }
        /// <summary>
        /// Class for double value
        /// </summary>
        public class ValueDouble : Value<double>
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public ValueDouble(double value) : base(value) { }
        }
        /// <summary>
        /// Class for string value
        /// </summary>
        public class ValueString : Value<string>
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public ValueString(string value) : base(value) { }
        }
        /// <summary>
        /// Class for list of values
        /// </summary>
        public class ValueList : Value<List<Value>>
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public ValueList(List<Value> value) : base(value) { }
            /// <summary>
            /// Equality check if made over the sequence of values
            /// </summary>
            /// <param name="other"></param>
            /// <returns></returns>
            public override bool Equals(Value other)
            {
                var otherT = other as ValueList;
                return otherT != null && V.SequenceEqual(otherT.V);
            }
            /// <summary>
            /// ToString overriden to dump comma separated values
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append('[');
                bool notFirst = false;
                foreach (var v in V)
                {
                    if (notFirst)
                        sb.Append(',');
                    notFirst = true;
                    sb.Append(v);
                }
                sb.Append(']');
                return string.Format(sb.ToString());
            }
        }
        #endregion // value
        /// <summary>
        /// Variable holding a value
        /// </summary>
        [DebuggerDisplay("{Name} = {Value}")]
        public class Variable
        {
            /// <summary>
            /// Name of the variable
            /// </summary>
            public string Name { get; private set; }
            /// <summary>
            /// Current value of the variable
            /// </summary>
            public Value Value { get; set; }
            /// <summary>
            /// Constructor
            /// </summary>
            public Variable(string name)
            {
                Name = name;
            }
        }

        /// <summary>
        /// Execution context for the script
        /// </summary>
        public class Context
        {
            /// <summary>
            /// Parent execution context for inner scopes
            /// </summary>
            public Context Parent { get; private set; }
            List<Variable> _variables = new List<Variable>();
            List<Func> _funcs = new List<Func>();
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="parent"></param>
            public Context(Context parent)
            {
                Parent = parent;
            }
            /// <summary>
            /// Get Variable from the context
            /// </summary>
            /// <param name="name">Variable name</param>
            /// <param name="searchParents">True to search in parent contexts as well</param>
            /// <returns>Reference to variable if found or null</returns>
            public Variable GetVariable(string name, bool searchParents)
            {
                return _variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)) ??
                        (searchParents && Parent != null ? Parent.GetVariable(name, true) : null);
            }
            /// <summary>
            /// Add new variable to the context
            /// </summary>
            /// <param name="name">Name of the variable</param>
            /// <param name="token">Optional token to be associated with the addition</param>
            /// <returns>Reference to newly added variable</returns>
            public Variable AddVariable(string name, object token = null)
            {
                var var = GetVariable(name, false);
                if (var != null)
                    throw ExecutionExceptionWithToken(token as Token, string.Format(_errfmtVariable_0_AlreadyDefined, name));

                var = new Variable(name);
                _variables.Add(var);
                return var;
            }
            /// <summary>
            /// Get function from the context
            /// </summary>
            /// <param name="name">Name of the function</param>
            /// <returns>Reference to function definition</returns>
            public Func GetFunc(string name)
            {
                return _funcs.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) ??
                        (Parent != null ? Parent.GetFunc(name) : null);
            }
            /// <summary>
            /// Add function to the context
            /// </summary>
            /// <param name="func"></param>
            /// <param name="token">Optional token to be associated with the addition</param>
            public void AddFunc(Func func, object token = null)
            {
                var var = GetFunc(func.Name);
                if (var != null)
                    throw ExecutionExceptionWithToken(token as Token, string.Format(_errfmtFunction_0_AlreadyDefined, func.Name));

                _funcs.Add(func);
            }
        }

        #region statements
        abstract class Stmt
        {
            public Token StartToken { get; private set; }
            public abstract void Execute(Context context);

            public Stmt(Token startToken)
            {
                StartToken = startToken;
            }

        }

        private static Stmt MatchStmt(ref Token token, bool insist)
        {
            var stmt = Stmt_DefVar.Match(ref token) ??
                       Stmt_Assign.Match(ref token) ??
                       Stmt_FuncCall.Match(ref token) ??
                       Stmt_For.Match(ref token) ??
                       Stmt_ForEach.Match(ref token) ??
                       Stmt_If.Match(ref token) ??
                       Stmt_While.Match(ref token) ??
                       Stmt_Block.Match(ref token, false) ??
                       Stmt_BreakOrContinue.Match(ref token) ??
                       (Stmt)Stmt_Return.Match(ref token);
            if (insist && stmt == null)
                throw ParserExceptionWithToken(token, _errStatementExpected);
            return stmt;
        }

        class Stmt_DefVar : Stmt
        {
            public string VarName { get; private set; }
            public Expr Expr { get; private set; }

            public Stmt_DefVar(Token startToken, string varName, Expr expr)
                : base(startToken)
            {
                VarName = varName;
                Expr = expr;
            }

            public static Stmt_DefVar Match(ref Token token)
            {
                var startToken = token;
                if (!MatchKeyword(ref token, "var"))
                    return null;

                // check for identifier
                var varName = MatchIdent(ref token, true);

                Expr expr = null;
                // check for =
                if (MatchSymbol(ref token, "=", false))
                {

                    // consume assignment expression
                    expr = MatchExpr(ref token, true);
                }

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_DefVar(startToken, varName, expr);
            }

            public override void Execute(Context context)
            {
                var var = context.AddVariable(VarName, StartToken);
                if (Expr != null)
                    var.Value = Expr.Calculate(context);
            }
        }

        class Stmt_Assign : Stmt
        {
            public Expr_Assign Expr { get; private set; }

            public Stmt_Assign(Token startToken, Expr_Assign expr)
                : base(startToken)
            {
                Expr = expr;
            }

            public static Stmt_Assign Match(ref Token token)
            {
                var startToken = token;
                var expr = Expr_Assign.Match(ref token);
                if (expr == null)
                    return null;

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_Assign(startToken, expr);
            }

            public override void Execute(Context context)
            {
                Expr.Execute(context);
            }
        }

        class Stmt_FuncCall : Stmt
        {
            public Expr_FuncCall Expr { get; private set; }

            public Stmt_FuncCall(Token startToken, Expr_FuncCall expr)
                : base(startToken)
            {
                Expr = expr;
            }

            public static Stmt_FuncCall Match(ref Token token)
            {
                var startToken = token;
                var expr = Expr_FuncCall.Match(ref token);
                if (expr == null)
                    return null;

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_FuncCall(startToken, expr);
            }

            public override void Execute(Context context)
            {
                Expr.Execute(context);
            }
        }

        class Stmt_For : Stmt
        {
            string _varName;
            Expr _expr1;
            Expr _expr2;
            Expr _expr3;
            Stmt Stmt;

            public Stmt_For(Token startToken, string varName, Expr expr1, Expr expr2, Expr expr3, Stmt stmt)
                : base(startToken)
            {
                _varName = varName;
                _expr1 = expr1;
                _expr2 = expr2;
                _expr3 = expr3;
                Stmt = stmt;
            }

            public static Stmt_For Match(ref Token token)
            {
                var startToken = token;
                if (!MatchKeyword(ref token, "for"))
                    return null;

                // match (
                MatchSymbol(ref token, "(", true);

                string varName = null;
                // check for local definition 
                if (MatchKeyword(ref token, "var"))
                {
                    // match var name
                    varName = MatchIdent(ref token, true);

                    // match =
                    MatchSymbol(ref token, "=", true);
                }

                // match 1st expr
                var expr1 = MatchExpr(ref token, true);

                // match ;
                MatchSymbol(ref token, ";", true);

                // match 2nd expr
                var expr2 = MatchExpr(ref token, true);

                // match ;
                MatchSymbol(ref token, ";", true);

                // match 3rd expr
                var expr3 = MatchExpr(ref token, true);

                // match )
                MatchSymbol(ref token, ")", true);

                // match statement
                var stmt = MatchStmt(ref token, true);

                return new Stmt_For(startToken, varName, expr1, expr2, expr3, stmt);
            }

            public override void Execute(Context context)
            {
                var innerContext = new Context(context);
                var value = _expr1.Calculate(innerContext);
                if (_varName != null)
                {
                    var var = innerContext.AddVariable(_varName, StartToken);
                    var.Value = value;
                }
                for (; _expr2.CheckCondition(innerContext); _expr3.Calculate(innerContext))
                {
                    try
                    {
                        Stmt.Execute(innerContext);
                    }
                    catch (BreakOrContinueException ex)
                    {
                        if (ex.Break)
                            break;
                    }
                }
            }
        }

        class Stmt_ForEach : Stmt
        {
            public string VarName { get; private set; }
            public Expr ExprColl { get; private set; }
            public Stmt Stmt { get; private set; }

            public Stmt_ForEach(Token startToken, string varName, Expr exprColl, Stmt stmt)
                : base(startToken)
            {
                VarName = varName;
                ExprColl = exprColl;
                Stmt = stmt;
            }

            public static Stmt_ForEach Match(ref Token token)
            {
                var startToken = token;
                if (!MatchKeyword(ref token, "foreach"))
                    return null;

                // match (
                MatchSymbol(ref token, "(", true);

                // match keyword 'var'
                MatchKeyword(ref token, "var");

                // match ident name
                var varName = MatchIdent(ref token, true);

                // match keyword 'in'
                MatchKeyword(ref token, "in");

                // match expr for collection
                var exprColl = MatchExpr(ref token, true);

                // match )
                MatchSymbol(ref token, ")", true);

                // match statement
                var stmt = MatchStmt(ref token, true);

                return new Stmt_ForEach(startToken, varName, exprColl, stmt);
            }

            public override void Execute(Context context)
            {
                var valueList = ExprColl.Calculate(context) as ValueList;
                if (valueList == null)
                    throw ExecutionExceptionWithToken(ExprColl.StartToken, _errExpressionNotCollection);
                var innerContext = new Context(context);
                var var = innerContext.AddVariable(VarName, StartToken);
                foreach (var value in valueList.V)
                {
                    var.Value = value;
                    try
                    {
                        Stmt.Execute(innerContext);
                    }
                    catch (BreakOrContinueException ex)
                    {
                        if (ex.Break)
                            break;
                    }
                }
            }
        }

        class Stmt_If : Stmt
        {
            public Expr Expr { get; private set; }
            public Stmt StmtThen { get; private set; }
            public Stmt StmtElse { get; private set; }

            public Stmt_If(Token startToken, Expr expr, Stmt stmtThen, Stmt stmtElse)
                : base(startToken)
            {
                Expr = expr;
                StmtThen = stmtThen;
                StmtElse = stmtElse;
            }

            public static Stmt_If Match(ref Token token)
            {
                var startToken = token;
                if (!MatchKeyword(ref token, "if"))
                    return null;

                // match (
                MatchSymbol(ref token, "(", true);

                // match cond expr
                var expr = MatchExpr(ref token, true);

                // match )
                MatchSymbol(ref token, ")", true);

                // match statement
                var stmtThen = MatchStmt(ref token, true);

                Stmt stmtElse = null;
                if (token != null && MatchKeyword(ref token, "else"))
                {

                    // match statement
                    stmtElse = MatchStmt(ref token, true);
                }
                return new Stmt_If(startToken, expr, stmtThen, stmtElse);
            }

            public override void Execute(Context context)
            {
                var innerContext = new Context(context);
                if (Expr.CheckCondition(innerContext))
                {
                    StmtThen.Execute(innerContext);
                }
                else
                {
                    if (StmtElse != null)
                        StmtElse.Execute(innerContext);
                }
            }
        }

        class Stmt_While : Stmt
        {
            public Expr Expr { get; private set; }
            public Stmt Stmt { get; private set; }

            public Stmt_While(Token startToken, Expr expr, Stmt stmt)
                : base(startToken)
            {
                Expr = expr;
                Stmt = stmt;
            }

            public static Stmt_While Match(ref Token token)
            {
                var startToken = token;
                if (!MatchKeyword(ref token, "while"))
                    return null;

                // match (
                MatchSymbol(ref token, "(", true);

                // match cond expr
                var expr = MatchExpr(ref token, true);

                // match )
                MatchSymbol(ref token, ")", true);

                // match statement
                var stmt = MatchStmt(ref token, true);

                return new Stmt_While(startToken, expr, stmt);
            }

            public override void Execute(Context context)
            {
                var innerContext = new Context(context);
                while (Expr.CheckCondition(innerContext))
                {
                    try
                    {
                        Stmt.Execute(innerContext);
                    }
                    catch (BreakOrContinueException ex)
                    {
                        if (ex.Break)
                            break;
                    }
                }
            }
        }

        class Stmt_Return : Stmt
        {
            public Expr Expr { get; private set; }

            public Stmt_Return(Token startToken, Expr expr)
                : base(startToken)
            {
                Expr = expr;
            }

            public static Stmt_Return Match(ref Token token)
            {
                var startToken = token;
                if (!MatchKeyword(ref token, "return"))
                    return null;

                // match expression optionally
                var expr = MatchExpr(ref token, false);

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_Return(startToken, expr);
            }

            public override void Execute(Context context)
            {
                Value value = Expr == null ? null : value = Expr.Calculate(context);
                throw new ReturnException(StartToken, value);
            }
        }

        class Stmt_BreakOrContinue : Stmt
        {
            public bool Break { get; private set; }

            public Stmt_BreakOrContinue(Token startToken, bool @break)
                : base(startToken)
            {
                Break = @break;
            }

            public static Stmt_BreakOrContinue Match(ref Token token)
            {
                var startToken = token;
                // check for both break and continue
                bool isBreak = MatchKeyword(ref token, "break");
                if (!isBreak && !MatchKeyword(ref token, "continue"))
                    return null;

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_BreakOrContinue(startToken, isBreak);
            }

            public override void Execute(Context context)
            {
                throw new BreakOrContinueException(StartToken, Break);
            }
        }

        class Stmt_Block : Stmt
        {
            public readonly List<Stmt> Stmts = new List<Stmt>();

            public Stmt_Block(Token startToken) : base(startToken) { }
            public static Stmt_Block Match(ref Token token, bool insist)
            {
                var startToken = token;
                if (!MatchSymbol(ref token, "{", insist))
                    return null;

                var block = new Stmt_Block(startToken);
                for (; ; )
                {
                    // check for )
                    if (MatchSymbol(ref token, "}", false))
                        break;

                    // consume statement
                    var stmt = MatchStmt(ref token, true);
                    block.Stmts.Add(stmt);
                }
                return block;
            }

            public override void Execute(Context context)
            {
                var innerContext = new Context(context);
                foreach (var stmt in Stmts)
                {
                    stmt.Execute(innerContext);
                }
            }
        }

        class Expr_FuncCall : Expr
        {
            public string FuncName { get; private set; }
            public List<Expr> ParamExprs { get; private set; }

            public Expr_FuncCall(Token startToken, string funcName)
                : base(startToken)
            {
                FuncName = funcName;

                ParamExprs = new List<Expr>();
            }

            public static Expr_FuncCall Match(ref Token token)
            {
                var startToken = token;
                // check for identifier
                var token_copy = token;
                var subName = MatchIdent(ref token_copy, false);
                if (subName == null)
                    return null;

                // insist on (
                if (!MatchSymbol(ref token_copy, "(", false))
                    return null;
                token = token_copy;

                var funcCall = new Expr_FuncCall(startToken, subName);

                // consume param expressions
                bool nextParam = false;
                for (; ; )
                {
                    // check for )
                    if (MatchSymbol(ref token, ")", false))
                        break;

                    if (nextParam)
                    {
                        // check for ,
                        MatchSymbol(ref token, ",", true);
                    }
                    nextParam = true;

                    // get param value
                    var valueExpr = MatchExpr(ref token, true);
                    funcCall.ParamExprs.Add(valueExpr);
                }

                return funcCall;
            }

            public override void Execute(Context context)
            {
                Call(context, false);
            }

            public override Value Calculate(Context context)
            {
                return Call(context, true);
            }

            private Value Call(Context context, bool insistResult)
            {
                var func = context.GetFunc(FuncName);
                if (func == null)
                    throw ExecutionExceptionWithToken(StartToken, string.Format(_errfmtFunction_0_NotDefined, FuncName));

                bool hasEllipsis = func.HasEllipsis;
                int paramCount = func.Params.Count;
                int exprCount = ParamExprs.Count;
                if (paramCount > exprCount || !hasEllipsis && paramCount != exprCount)
                    throw ExecutionExceptionWithToken(StartToken, _errParametersCountMismatch);

                // calculate parameters
                var innerContext = new Context(context);
                int param;
                for (param = 0; param < paramCount; param++)
                {
                    var var = innerContext.AddVariable(func.Params[param], StartToken);
                    var.Value = ParamExprs[param].Calculate(context);
                }
                Debug.Assert(param == exprCount || hasEllipsis);

                // in case function has ellipsis add remaining params as a list
                if (hasEllipsis)
                {
                    var remainingValues = new List<Value>();
                    for (; param < exprCount; param++)
                    {
                        var value = ParamExprs[param].Calculate(context);
                        remainingValues.Add(value);
                    }
                    var var = innerContext.AddVariable("...", StartToken);
                    var.Value = new ValueList(remainingValues);
                }

                var result = func.Execute(innerContext);

                if (insistResult && result == null)
                    throw ExecutionExceptionWithToken(StartToken, _errFunctionShouldReturnValue);

                return result;
            }
        }
        #endregion statements

        #region primitives
        private static string MatchIdent(ref Token token, bool insist)
        {
            if (token == null)
                throw new ParserException(_errUnexpectedEndOfFile);
            if (token.Type == TokenType.Identifier)
            {
                var value = token.Value;
                token = token.Next;
                return value;
            }
            if (insist)
                throw ParserExceptionWithToken(token, _errIdentifierExpected);
            else
                return null;
        }

        private static bool MatchSymbol(ref Token token, string symbol, bool insist)
        {
            if (token == null)
                throw new ParserException(_errUnexpectedEndOfFile);
            if (token.Type == TokenType.Symbol && token.Value == symbol)
            {
                token = token.Next;
                return true;
            }

            if (insist)
                throw ParserExceptionWithToken(token, string.Format(_errfmt_0_Expected, symbol));
            else
                return false;
        }

        private static bool MatchKeyword(ref Token token, string keyword)
        {
            if (token == null)
                throw new ParserException(_errUnexpectedEndOfFile);
            if (token.Type == TokenType.Keyword && token.Value == keyword)
            {
                token = token.Next;
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion primitives

        #region functions
        /// <summary>
        /// Base class for functions
        /// </summary>
        public abstract class Func
        {
            /// <summary>
            /// Name of the function
            /// </summary>
            public string Name { get; private set; }
            /// <summary>
            /// List of parameter names
            /// </summary>
            public List<string> Params { get; private set; }
            /// <summary>
            /// True if function has variable parameter count
            /// </summary>
            public bool HasEllipsis { get; private set; }
            /// <summary>
            /// Constructor
            /// </summary>
            public Func(string name, bool hasEllipsis)
            {
                Name = name;
                HasEllipsis = hasEllipsis;
                Params = new List<string>();
            }

            /// <summary>
            /// Abstract method called when executing the function
            /// </summary>
            public abstract Value Execute(Context context);
        }

        class Func_ToString : Func
        {
            public Func_ToString()
                : base("to_string", false)
            {
                Params.Add("_");
            }

            public override Value Execute(Context context)
            {
                var var = context.GetVariable("_", false);
                Debug.Assert(var != null);
                var value = var.Value;
                Debug.Assert(value != null);
                // in case input is string just return it
                var sValue = value as Value<string>;
                if (sValue != null)
                    return sValue;
                // otherwise generate new string value from input value
                return new ValueString(value.ToString());
            }
        }

        class Func_ToInt : Func
        {
            public Func_ToInt()
                : base("to_int", false)
            {
                Params.Add("_");
            }

            public override Value Execute(Context context)
            {
                var var = context.GetVariable("_", false);
                Debug.Assert(var != null);
                var value = var.Value;
                Debug.Assert(value != null);

                // if input is int just return it
                var iValue = var.Value as ValueInt;
                if (iValue != null)
                    return iValue;

                // convert double to int
                var dValue = var.Value as ValueDouble;
                if (dValue != null)
                    return new ValueInt((int)dValue.V);

                // convert bool to 0 or -1
                var bValue = var.Value as ValueBool;
                if (bValue != null)
                    return new ValueInt(bValue.V ? -1 : 0);

                throw new ExecutionException(_errUnableToConvertToIngeger);
            }
        }

        class Func_CountOf : Func
        {
            public Func_CountOf()
                : base("count_of", false)
            {
                Params.Add("_");
            }

            public override Value Execute(Context context)
            {
                var var = context.GetVariable("_", false);
                Debug.Assert(var != null);
                var value = var.Value;
                Debug.Assert(value != null);

                // if input is int just return it
                var lValue = var.Value as ValueList;
                if (lValue == null)
                    throw new ExecutionException(_errExpressionNotCollection);

                return new ValueInt(lValue.V.Count);
            }
        }

        class Func_With_Block : Func
        {
            public Token StartToken { get; private set; }

            public Stmt Block { get; private set; }

            public Func_With_Block(Token startToken, string name, bool hasEllipsis, Stmt block)
                : base(name, hasEllipsis)
            {
                StartToken = startToken;
                Block = block;
            }

            public static Func_With_Block Match(ref Token token)
            {
                var startToken = token;
                // should start with sub
                if (!MatchKeyword(ref token, "func"))
                    return null;

                // insist for sub name
                var name = MatchIdent(ref token, true);

                // insist for (
                MatchSymbol(ref token, "(", true);

                var @params = new List<string>();
                bool nextParam = false;
                bool hasEllipsis = false;
                for (; ; )
                {
                    // check for )
                    if (MatchSymbol(ref token, ")", false))
                        break;

                    // found ellipsis but there are more parameters
                    if (hasEllipsis)
                        throw ParserExceptionWithToken(token, _errEllipsisShouldBeLast);

                    if (nextParam)
                    {
                        // check for ,
                        MatchSymbol(ref token, ",", true);
                    }
                    nextParam = true;

                    if (MatchSymbol(ref token, "...", false))
                    {
                        hasEllipsis = true;
                        continue;
                    }

                    // get param name
                    var param = MatchIdent(ref token, true);
                    @params.Add(param);
                }

                // block should follow
                var block = Stmt_Block.Match(ref token, true);
                var func = new Func_With_Block(startToken, name, hasEllipsis, block);
                func.Params.AddRange(@params);
                return func;
            }

            public override Value Execute(Context context)
            {
                try
                {
                    Block.Execute(context);
                    return null;
                }
                catch (BreakOrContinueException ex)
                {
                    throw ExecutionExceptionWithToken(ex.Token, ex.Break ? _errBreakWithoutSurroundingForOrWhile : _errContinueWithoutSurroundingForOrWhile);
                }
                catch (ReturnException ex)
                {
                    return ex.Value;
                }
            }
        }
        #endregion // functions

        /// <summary>
        /// Instance of script
        /// </summary>
        public class Script
        {
            static Func[] _internalFuncs = new Func[]
            {
                new Func_CountOf(),
                new Func_ToInt(),
                new Func_ToString(),
            };
            List<Func_With_Block> _funcs = new List<Func_With_Block>();
            Stmt_Block _stmt = new Stmt_Block(null);

            /// <summary>
            /// Execution method
            /// </summary>
            /// <param name="context"></param>
            public void Execute(Context context)
            {
                var innerContext = new Context(context);
                // add internal funcs to the context
                foreach (var func in _internalFuncs)
                    innerContext.AddFunc(func);
                // add functions defined in the script to the context
                foreach (var func in _funcs)
                    innerContext.AddFunc(func, func.StartToken);
                // execute the script
                _stmt.Execute(innerContext);
            }
            // Parse script from tokens
            static Script Parse(Token token)
            {
                var script = new Script();
                while (token != null)
                {
                    var stmt = MatchStmt(ref token, false);
                    if (stmt != null)
                    {
                        script._stmt.Stmts.Add(stmt);
                        continue;
                    }
                    var func = Func_With_Block.Match(ref token);
                    if (func != null)
                    {
                        script._funcs.Add(func);
                        continue;
                    }
                    if (token != null)
                    {
                        throw ParserExceptionWithToken(token, string.Format(_errfmtUnexpectedToken_0_, token.Value));
                    }
                }

                return script;
            }
            /// <summary>
            /// Parse script as single string
            /// </summary>
            public static Script Parse(string text, string scriptName)
            {
                var token = Tokenize(text, scriptName);
                return Parse(token);
            }
            /// <summary>
            /// Parse script as array of lines
            /// </summary>
            public static Script Parse(string[] lines, string scriptName)
            {
                var token = Tokenize(lines, scriptName);
                return Parse(token);
            }
        }
    }
}

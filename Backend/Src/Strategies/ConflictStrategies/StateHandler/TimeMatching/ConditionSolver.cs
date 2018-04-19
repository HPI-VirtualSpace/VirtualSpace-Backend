using System;
using System.Collections.Generic;
using Gurobi;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class ConditionSolver
    {
        private GRBModel _model;
        private Dictionary<int, GRBVar> _variables;
        private Dictionary<int, GRBVar> _arrivalTimes;
        private GRBVar _prepS;
        private GRBVar _execS;
        private GRBLinExpr _value;
        private int _currentUserId;
        GRBEnv _env;

        public const int MaxVariablesPerUser = 1000;

        public ConditionSolver()
        {
            _env = new GRBEnv();
            _env.LogToConsole = 0;

            Create();
        }

        public void Create()
        {
            
            _model = new GRBModel(_env);
            _variables = new Dictionary<int, GRBVar>();
            _arrivalTimes = new Dictionary<int, GRBVar>();

        }

        public void Reset()
        {
            Create();

            _model.Reset();

            _variables.Clear();
            _arrivalTimes.Clear();

            _value = 0;

            _prepS = _model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, "prep");
            _execS = _model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, "exec");

            _variables[(int)VariableTypes.PreperationTime] = _prepS;
            _variables[(int)VariableTypes.ExecutionTime] = _execS;
        }

        public bool TrySolve(out double prepS, out double execS)
        {
            _model.SetObjective(_value);
            _model.Parameters.DualReductions = 0;
            _model.Update();
            _model.Write("debug.lp");

            _model.Optimize();

            if (_model.Status != GRB.Status.OPTIMAL)
            {
                Logger.Warn($"Didn't find solution to time conditions. Gurobi status: {_model.Status}");
                prepS = -1;
                execS = -1;
                return false;
            }

            try
            {
                prepS = _prepS.X;
                execS = _execS.X;
            } catch
            {
                Logger.Warn($"Exception. Gurobi status: {_model.Status}");
                prepS = -1;
                execS = -1;
                return false;
            }

            return true;
        }

        internal void AddCalculationTime(double calculationSeconds)
        {
            var calculationVar = _model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, "calculationTime");
            _model.AddConstr(calculationSeconds == calculationVar, "calculationTimeConstraint");
            _variables[(int)VariableTypes.CalculationTime] = calculationVar;
        }

        internal void AddArrivalTimeForUser(double arrivalSeconds, int userId)
        {
            var arrivalVar = _model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, "arrivalTime_" + userId);
            _model.AddConstr(arrivalSeconds == arrivalVar, "arrivalTimeConstraint_" + userId);
            _arrivalTimes[userId] = arrivalVar;
        }

        internal void AddValueFunction(int userNum, float priority, Value valueFunction)
        {
            if (!Value.IsDefined(valueFunction)) return;

            var gurobiValue = ConvertToGurobi(valueFunction);

            _value += priority * gurobiValue;
        }

        public void AddConditions(int userId, List<TimeCondition> conditions, bool debugPrint=false)
        {
            if (conditions == null) return;

            _currentUserId = userId;
            int nextUserConditionId = 0;
            
            foreach (var condition in conditions)
            {
                Bound bound = condition as Bound;

                if (bound != null)
                {
                    if (debugPrint)
                        Logger.Debug(bound.ToString());

                    GRBTempConstr gurobiBound;
                    switch (bound.BoundType)
                    {
                        case BoundType.Equal:
                            gurobiBound = ConvertToGurobi(bound.Left) == ConvertToGurobi(bound.Right);
                            break;
                        case BoundType.SmallerEqual:
                            gurobiBound = ConvertToGurobi(bound.Left) <= ConvertToGurobi(bound.Right);
                            break;
                        case BoundType.NotEqual:
                            gurobiBound = ConvertToGurobi(bound.Left) != ConvertToGurobi(bound.Right);
                            break;
                        default:
                            throw new ArgumentException("Conditions: Unhandled bound type");
                    }

                    _model.AddConstr(gurobiBound, $"condition_u{_currentUserId}_n{nextUserConditionId++}");
                }
                else
                {
                    throw new ArgumentException("Condition: Unhandled condition type");
                }
            }
        }

        private GRBLinExpr ConvertToGurobi(Value value)
        {
            if (value is Constant)
            {
                return ConvertToGurobi((Constant) value);
            }

            if (value is Variable)
            {
                return ConvertToGurobi((Variable) value);
            }

            if (value is Expression)
            {
                Expression expression = (Expression) value;

                if (expression.OperationType == OperationType.Multiply)
                {
                    if (expression.Left is Variable && expression.Right is Constant)
                    {
                        return ConvertToGurobi(expression.Left) * ConvertToGurobi((Constant)expression.Right);
                    } else if (expression.Left is Constant && expression.Right is Variable)
                    {
                        return ConvertToGurobi((Constant)expression.Left) * ConvertToGurobi(expression.Right);
                    }
                    else
                    {
                        throw new ArgumentException("Trying to multiply two linear expressions.");
                    }
                }

                var left = ConvertToGurobi(expression.Left);
                var right = ConvertToGurobi(expression.Right);

                if (expression.OperationType == OperationType.Plus)
                {
                    return left + right;
                }
                else
                {
                    return left - right;
                }
            }

            return null;
        }

        private double ConvertToGurobi(Constant c)
        {
            return c.Value;
        }

        private GRBVar ConvertToGurobi(Variable v)
        {
            int systemVariableId;

            switch (v.Type)
            {
                case VariableTypes.PreperationTime:
                case VariableTypes.ExecutionTime:
                case VariableTypes.CalculationTime:
                    systemVariableId = (int)v.Type;
                    break;
                case VariableTypes.ArrivalTime:
                    return _arrivalTimes[_currentUserId];
                default:
                    systemVariableId = (_currentUserId + 1) * MaxVariablesPerUser + v.VariableId;
                    break;
            }

            GRBVar returnVar;
            if (_variables.ContainsKey(systemVariableId))
            {
                returnVar = _variables[systemVariableId];
            }
            else
            {
                returnVar = _variables[systemVariableId] = CreateNewGurobiVariable(v);
            }

            return returnVar;
        }

        private GRBVar CreateNewGurobiVariable(Variable v)
        {
            var grbType = v.Type == VariableTypes.Integer ? GRB.INTEGER : GRB.CONTINUOUS;
            string grbName = $"{v.Type}-u{_currentUserId}-v{v.VariableId}";
            
            return _model.AddVar(-double.PositiveInfinity, double.PositiveInfinity, 0, grbType, grbName);
        }
    }
}
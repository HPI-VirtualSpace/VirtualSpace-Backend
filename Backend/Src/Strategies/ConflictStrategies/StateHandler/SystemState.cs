using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    class SystemState
    {
        #region Variables
        #region StateHandling
        private const int NumRotationStates = 8;
        private const int NoFocus = NumRotationStates;

        private int _rotationState;
        private int _playerInFocusState;
        // substate in complex transition
        private int _switchState = 0;
        private List<int> _originalUserNumToIndex;
        private List<int> _userNumToIndex;
        private int _stateId;
        private int _actualNumUsers;
        // NumRotationStates * NumDiagonalStates
        private Vector[][] _statePositions;
        #endregion

        #region Dependencies
        private readonly VoronoiSplitter _voronoi;
        #endregion

        #region SwitchHelper
        private static readonly VSUserState[] NormalStates =
            {
                VSUserState.UpLeft, VSUserState.Up,
                VSUserState.UpRight, VSUserState.Right,
                VSUserState.DownRight, VSUserState.Down,
                VSUserState.DownLeft, VSUserState.Left
            };
        private static readonly VSUserState[] DefocusStates =
            {VSUserState.UpLeftDefocus, VSUserState.UpRightDefocus, VSUserState.DownRightDefocus, VSUserState.DownLeftDefocus,};
        private static readonly VSUserState[] FocusStates =
            {VSUserState.UpLeftFocus, VSUserState.UpRightFocus, VSUserState.DownRightFocus, VSUserState.DownLeftFocus,};

        private static readonly Dictionary<StateTransition, int> FocusStateToPlayerIndex =
            new Dictionary<StateTransition, int>
            {
                { StateTransition.Focus1, 0 },
                { StateTransition.Focus2, 2 },
                { StateTransition.Focus3, 4 },
                { StateTransition.Focus4, 6 }
            };

        private static readonly Dictionary<StateTransition, int> SwitchStateToPlayerIndex =
            new Dictionary<StateTransition, int>
            {
                { StateTransition.Switch1, 0 },
                { StateTransition.Switch2, 2 },
                { StateTransition.Switch3, 4 },
                { StateTransition.Switch4, 6 }
            };
        #endregion
        #endregion

        #region Creation
        public SystemState(VoronoiSplitter voronoi)
        {
            _rotationState = 0;
            _playerInFocusState = NoFocus;
            _stateId = 0;
            
            _voronoi = voronoi;

            SetInitialUserNumToIndex(new int[0]);
        }

        public int CurrentStateId => _stateId;
        public Vector[] RegularPositions => _statePositions[1].Where((elem, i) => i % 2 == 0).ToArray();

        private const float SwitchOffset = .4f;
        private readonly Vector[] _switchOffsets = {new Vector(0, -SwitchOffset), new Vector(-SwitchOffset, 0), new Vector(0, SwitchOffset), new Vector(SwitchOffset, 0) };
        public void InitializeStatePositions(
            double minX, double minY,
            double maxX, double maxY,
            double regularOffsetFromBounds, double defocusOffsetFromBounds)
        {
            Vector[] regularStatePositions = SetRotationalPositions(regularOffsetFromBounds, minX, minY, maxX, maxY);
            Vector[] defocusStatePositions = SetCornersWithOffset(regularOffsetFromBounds, minX, minY, maxX, maxY);
            Vector[] focusStatePositions = SetCornersWithOffset((maxX - minX) / 2, minX, minY, maxX, maxY);
            //Vector[] switchDefocusStatePositions = SetCornersWithOffset((maxX - minX) / 2, minX, minY, maxX, maxY);
            Vector[] switchFocusStatePositions = SetCornersWithOffset((maxX - minX) / 2, minX, minY, maxX, maxY);
            for (int switchNum = 0; switchNum < 4; switchNum++)
            {
                var switchVector = _switchOffsets[switchNum];
                switchFocusStatePositions[switchNum] += switchVector;
            }

            _statePositions = new[] { defocusStatePositions, regularStatePositions, focusStatePositions, switchFocusStatePositions };
        }

        private static Vector[] SetRotationalPositions(double offset,
            double minX, double minY,
            double maxX, double maxY)
        {
            Vector[] statePositions = new Vector[8];
            var leftX = minX * 3 / 4 + maxX / 4;
            var midX = minX / 2 + maxX / 2;
            var rightX = minX / 4 + maxX * 3 / 4;
            var leftY = minY * 3 / 4 + maxY / 4;
            var midY = minY / 2 + maxY / 2;
            var rightY = minY / 4 + maxY * 3 / 4;
            statePositions[0] = new Vector(leftX, leftY);
            statePositions[1] = new Vector(midX, leftY);
            statePositions[2] = new Vector(rightX, leftY);
            statePositions[3] = new Vector(rightX, midY);
            statePositions[4] = new Vector(rightX, rightY);
            statePositions[5] = new Vector(midX, rightY);
            statePositions[6] = new Vector(leftX, rightY);
            statePositions[7] = new Vector(leftX, midY);
            return statePositions;
        }

        public void SetInitialUserNumToIndex(int[] userNumToIndex)
        {
            _actualNumUsers = userNumToIndex.Length;
            _originalUserNumToIndex = userNumToIndex.ToList();
            _userNumToIndex = userNumToIndex.ToList();
            //UpdateOwnership();
        }

        private static Vector[] SetCornersWithOffset(double offsetFromBounds,
            double minX, double minY,
            double maxX, double maxY)
        {
            return SetCornersWithOffset(offsetFromBounds, offsetFromBounds, minX, minY, maxX, maxY);
        }

        private static Vector[] SetCornersWithOffset(double offsetX, double offsetY,
            double minX, double minY,
            double maxX, double maxY)
        {
            Vector[] statePositions = new Vector[4];
            statePositions[0] = new Vector(minX + offsetX, minY + offsetY);
            statePositions[1] = new Vector(maxX - offsetX, minY + offsetY);
            statePositions[2] = new Vector(maxX - offsetX, maxY - offsetY);
            statePositions[3] = new Vector(minX + offsetX, maxY - offsetY);
            return statePositions;
        }

        public void Reset()
        {
            _rotationState = 0;
            _playerInFocusState = NoFocus;
            _stateId = 0;
            _userNumToIndex = _originalUserNumToIndex.ToList();
        }
        #endregion

        #region Transitions
        private void RotateRight()
        {
            // assert(DiagonalState == 1)
            _rotationState = (_rotationState - 2 + NumRotationStates) % NumRotationStates;
        }

        private void RotateLeft()
        {
            // assert(DiagonalState == 1)
            _rotationState = (_rotationState + 2 + NumRotationStates) % NumRotationStates;
        }

        internal void PrintUserIndices()
        {
            for (int userNum = 0; userNum < _actualNumUsers; userNum++)
            {
                Logger.Debug($"{userNum} has {GetRotationalIndexFromUserNum(userNum)} (Rot) and {_userNumToIndex[userNum]} (Index)");
            }
        }

        public List<int> GetCurrentUserNumToIndex()
        {
            return _userNumToIndex.ToList();
        }

        private void Rotate45Right()
        {
            _rotationState = (_rotationState - 1 + NumRotationStates) % NumRotationStates;
        }

        private void Rotate45Left()
        {
            _rotationState = (_rotationState + 1 + NumRotationStates) % NumRotationStates;
        }

        private void Focus(int focusPlayerIndex)
        {
            //Logger.Debug($"Focusing on {focusPlayerIndex}");
            // assert(DiagonalState == 1)
            _playerInFocusState = focusPlayerIndex;
        }

        private void Defocus()
        {
            // assert(DiagonalState != NoFocus)
            _playerInFocusState = NoFocus;
        }

        private bool _switch;
        public bool Transition(StateTransition transition, List<VSUserTransition> userTransitions)
        {
            switch (transition)
            {
                case StateTransition.Focus1:
                case StateTransition.Focus2:
                case StateTransition.Focus3:
                case StateTransition.Focus4:
                {
                    Focus(FocusStateToPlayerIndex[transition]);
                    break;
                }
                case StateTransition.Unfocus:
                {
                    Defocus();
                    break;
                }
                case StateTransition.RotateLeft:
                {
                    RotateLeft();
                    break;
                }
                case StateTransition.RotateRight:
                {
                    RotateRight();
                    break;
                }
                case StateTransition.Rotate45Left:
                    Rotate45Left();
                    break;
                case StateTransition.Rotate45Right:
                    Rotate45Right();
                    break;
                case StateTransition.Stay:
                {
                    break;
                }
                case StateTransition.Switch1:
                case StateTransition.Switch2:
                case StateTransition.Switch3:
                case StateTransition.Switch4:
                    if (_switchState == 0)
                    {
                        _switch = true;
                        int userIndexInSwitchToFocus = SwitchStateToPlayerIndex[transition];
                        Focus(userIndexInSwitchToFocus);

                        _switchState++;

                        return true;
                    }
                    else if (_switchState == 1)
                    {
                        int userIndexInSwitchToFocus = SwitchStateToPlayerIndex[transition];
                        int userIndexInSwitchToRotate = GetAdjacentUserIndex(userIndexInSwitchToFocus, 2);

                        // todo this fails because of the rotational index
                        int userNumFocus = _userNumToIndex.IndexOf((userIndexInSwitchToFocus - _rotationState + NumRotationStates) % NumRotationStates);
                        int userNumRotate = _userNumToIndex.IndexOf((userIndexInSwitchToRotate - _rotationState + NumRotationStates) % NumRotationStates);

                        _userNumToIndex[userNumFocus] = userIndexInSwitchToRotate;
                        _userNumToIndex[userNumRotate] = userIndexInSwitchToFocus;
                        _playerInFocusState = userIndexInSwitchToRotate;

                        _switchState++;

                        return true;
                    }
                    else if (_switchState == 2)
                    {
                        _switch = false;
                        Defocus();

                        _switchState = 0;
                    }

                    break;
                case StateTransition.AssymmetricRotation:
                    for (int userNum = 0; userNum < _actualNumUsers; userNum++)
                    {
                        // change respective user indices
                        var userTransition = userTransitions[userNum];
                        var oldUserIndex = GetRotationalIndexFromUserNum(userNum);
                        var newUserIndex = GetRotationalIndexFromUserNum(userNum, userTransition);
                        //Logger.Debug($"user {userNum} going from index {oldUserIndex} to {newUserIndex} with {userTransition}");

                        // todo this is not right for transitioning, it includes the rotational index
                        _userNumToIndex[userNum] = (newUserIndex - _rotationState + 8) % 8;
                        //Logger.Debug($"test: now at index {GetRotationalIndexFromUserNum(userNum)}");

                    }

                    break;
                default:
                    Logger.Warn("Transition not handled");
                    break;
            }

            _stateId++;
            //Logger.Debug("Increased state id to " + _stateId);

            return false;
        }

        /**
         * Warning: Don't use this to actually modify the state.
         * This method only used as a simulation with a reset afterwards.
         */
        private void UserCentricTransition(VSUserTransition transition, int playerNum)
        {
            switch (transition)
            {
                case VSUserTransition.Stay:
                    break;
                case VSUserTransition.Focus:
                    Focus(_userNumToIndex[playerNum] + _rotationState);
                    break;
                case VSUserTransition.Unfocus:
                    Defocus();
                    break;
                case VSUserTransition.Defocus:
                    Focus(_userNumToIndex[playerNum] + _rotationState == 0 ? 1 : 0);
                    break;
                case VSUserTransition.Undefocus:
                    Defocus();
                    break;
                case VSUserTransition.RotateLeft:
                case VSUserTransition.SwitchLeft:
                    RotateLeft();
                    break;
                case VSUserTransition.RotateRight:
                case VSUserTransition.SwitchRight:
                    RotateRight();
                    break;
                case VSUserTransition.Rotate45Left:
                    Rotate45Left();
                    break;
                case VSUserTransition.Rotate45Right:
                    Rotate45Right();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(transition), transition, null);
            }
        }
        
        #endregion

        #region Getter
        public bool IsFocused()
        {
            return _playerInFocusState != NoFocus;
        }
        
        public void GetCurrentState(int playerNum, out VSUserState currentState, out List<VSUserTransition> options)
        {
            int playerIndex = _userNumToIndex[playerNum];

            int rotationIndex = GetRotationIndexFromUserIndex(playerIndex);

            if (_playerInFocusState == playerIndex)
            {
                currentState = FocusStates[rotationIndex % 2];
                options = new List<VSUserTransition> { VSUserTransition.Unfocus, VSUserTransition.Stay };
            }
            else if (_playerInFocusState < NoFocus)
            {
                currentState = DefocusStates[rotationIndex % 2];
                options = new List<VSUserTransition> { VSUserTransition.Undefocus, VSUserTransition.Stay };
            }
            else
            {
                currentState = NormalStates[rotationIndex];
                options =
                    new List<VSUserTransition>
                    {
                        VSUserTransition.RotateLeft,
                        VSUserTransition.RotateRight,
                        VSUserTransition.Rotate45Left,
                        VSUserTransition.Rotate45Right,
                        VSUserTransition.Stay
                    };

                if (((playerIndex + _rotationState + NumRotationStates) % NumRotationStates) % 2 == 0)
                {
                    options.Add(VSUserTransition.Focus);
                    options.Add(VSUserTransition.Defocus);
                    options.Add(VSUserTransition.SwitchLeft);
                    options.Add(VSUserTransition.SwitchRight);
                }
            }
        }

        public StateInfo GetCurrentState(int playerNum)
        {
            GetCurrentState(playerNum, out VSUserState currentState, out List<VSUserTransition> options);
            GetPlayerStatus(playerNum, out Vector endTransitionPosition, out Polygon endTransitionArea);

            int rotationState = _rotationState;
            int focusState = _playerInFocusState;

            List<Vector> endPositions = new List<Vector>();
            List<Polygon> endAreas = new List<Polygon>();
            foreach (VSUserTransition transition in options)
            {
                // reset the state after the user centric transition
                _rotationState = rotationState;
                _playerInFocusState = focusState;
                // we need to undo this here
                // otherwise it's going to fuck up the switched index positions?
                // no because Assymmetric will never be called here
                UserCentricTransition(transition, playerNum);

                // todo this needs to be updated so that ghost areas are adequately transmitted

                GetPlayerStatus(playerNum, out Vector position, out Polygon area);
                endPositions.Add(position);
                endAreas.Add(area);
            }

            _rotationState = rotationState;
            _playerInFocusState = focusState;

            Logger.Debug($"Creating state info with state id {_stateId}");
            StateInfo info = new StateInfo()
            {
                YourFinalState = currentState,
                PossibleTransitions = options,
                ThisTransitionEndPosition = endTransitionPosition,
                ThisTransitionEndArea = endTransitionArea,
                TransitionEndPositions = endPositions,
                TransitionEndAreas = endAreas,
                StateId = _stateId
            };
            return info;
        }

        public void GetPositionsForCurrentState(out List<List<Vector>> positions)
        {
            positions = new List<List<Vector>>();
            for (int userNum = 0; userNum < _actualNumUsers; userNum++)
            {
                positions.Add(GetPositionAndComplements(userNum));
            }
        }

        bool IsInTheCenter(Vector pos) => Math.Abs(pos.X) < .5f && Math.Abs(pos.Z) < .5f;
        
        private void GenerateComplementAreas(int playerNum,
            out Vector centroid, out Polygon area)
        {
            GetPositionsForCurrentState(out List<List<Vector>> positions);

            var userMocks = positions[playerNum];

            var mockUserWeights = userMocks.Select
                (pos => IsInTheCenter(pos) ? 2D : 1D).ToList();

            var userWeightedMocks = VoronoiWeighting.WeightPositions(userMocks, mockUserWeights);

            var userAreas = _voronoi.GetAreasForPositions(userWeightedMocks);

            centroid = userAreas?[0].Centroid;
            area = userAreas?[0];
        }

        private void GetPlayerStatus(int playerNum, out Vector position, out Polygon area)
        {
            GenerateComplementAreas(playerNum, out position, out area);

            //position = positions[playerNum];
            //area = areas[playerNum];
        }

        private int GetRotationIndexFromUserIndex(int userIndex, int offset=0)
        {
            return (_rotationState + userIndex + NumRotationStates + offset) % NumRotationStates;
        }

        public Vector GetPosition(int playerNum)
        {
            int playerIndex = _userNumToIndex[playerNum];
            int rotationIndex = GetRotationIndexFromUserIndex(playerIndex);

            int diagonalIndex;
            if (_playerInFocusState == NoFocus) diagonalIndex = 1;
            else diagonalIndex = _playerInFocusState == playerIndex ? (_switch ? 3 : 2) : 0;

            return _statePositions[diagonalIndex][diagonalIndex != 1 ? rotationIndex / 2 : rotationIndex];
        }

        /// <summary>
        /// The first position in the list is the users position. The rest are mock positions.
        /// </summary>
        public List<Vector> GetPositionAndComplements(int userNum)
        {
            int playerIndex = _userNumToIndex[userNum];
            int rotationIndex = GetRotationIndexFromUserIndex(playerIndex);

            var indexOffset = 2;
            var positions = new List<Vector>();
            for (int genUserNum = 0; genUserNum < 4; genUserNum++)
            {
                //var genPlayerIndex = (playerIndex + genUserNum * indexOffset) % NumRotationStates;
                var genRotationIndex = (rotationIndex + genUserNum * indexOffset) % NumRotationStates;

                int diagonalIndex;
                if (_playerInFocusState == NoFocus) diagonalIndex = 1;
                else diagonalIndex = _playerInFocusState == genRotationIndex ? (_switch ? 3 : 2) : 0;

                bool noSubTransitions = diagonalIndex != 1;
                var correctedRotationIndex = noSubTransitions ? genRotationIndex / 2 : genRotationIndex;
                
                positions.Add(_statePositions[diagonalIndex][correctedRotationIndex]);
            }

            return positions;
        }

        private int GetAdjacentUserIndex(int userIndex, int offset = 0)
        {
            return (userIndex + offset + NumRotationStates) % NumRotationStates;
        }

        public int GetRotationalIndexFromUserNum(int userNum, int offset=0)
        {
            return GetRotationIndexFromUserIndex(_userNumToIndex[userNum], offset);
        }

        public int GetRotationalIndexFromUserNum(int userNum, VSUserTransition transition)
        {
            int offset = Offset(transition);

            return GetRotationalIndexFromUserNum(userNum, offset);
        }

        private static int Offset(VSUserTransition transition)
        {
            int offset = 0;
            switch (transition)
            {
                case VSUserTransition.Rotate45Left:
                    offset = 1;
                    break;
                case VSUserTransition.Rotate45Right:
                    offset = -1;
                    break;
                case VSUserTransition.RotateRight:
                    offset = -2;
                    break;
                case VSUserTransition.RotateLeft:
                    offset = 2;
                    break;
            }

            return offset;
        }

        #endregion

        public bool HaveEvenLayout()
        {
            return HaveEvenLayout(_userNumToIndex);
        }

        // todo only need a distance of two
        private static bool HaveEvenLayout(List<int> userNumToIndex)
        {
            //Logger.Debug($"Is {userNumToIndex.ToPrintableString()} even?");

            if (userNumToIndex.Count < 2)
            {
                //Logger.Debug("Even");
                return true;
            }

            Func<int, bool> isIndexEven = i => Math.Abs(userNumToIndex[i]) % 2 == 0;

            // check what the first index does
            bool shouldBeEven = isIndexEven(0);

            for (int i = 1; i < userNumToIndex.Count; i++)
            {
                bool isEven = isIndexEven(i);
                if (shouldBeEven && !isEven || !shouldBeEven && isEven)
                {
                    //Logger.Debug("Not even");
                    return false;
                }
            }

            //Logger.Debug("Even");
            return true;
        }

        public bool HaveEvenLayoutAfter(VSUserTransition[] userTransitions)
        {
            var userNumToIndex = _userNumToIndex.ToList();

            for (int userNum = 0; userNum < userTransitions.Length; userNum++)
            {
                var transition = userTransitions[userNum];
                var transitionOffset = Offset(transition);
                userNumToIndex[userNum] = (userNumToIndex[userNum] + transitionOffset) % 8;
            }

            return HaveEvenLayout(userNumToIndex);
        }
    }
}

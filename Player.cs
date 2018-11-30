using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

/**
 * Send your busters out into the fog to trap ghosts and bring them home!
 **/
namespace Klee
{
    class Player
    {
        private static bool _isRadarUsed = false;
        private static int _currStunDelay = 0;
        private static int _caughtGhosts = 0;

        private static Random _rnd = new Random();
        const int WIDTH = 16001;
        const int HEIGHT = 9001;

        private const int MIN_GHOST_DIST = 900;
        private const int MAX_GHOST_DIST = 1760;
        private const int RELEASE_DIST = 1600;
        private const int STUN_DELAY = 20;
        private const int VISIBLE_RANGE = 2200;
        private const int BUSTER_SPEED = 800;
        private const int GHOST_SPEED = 400;
        private const int GHOST_VISIBLE_RANGE = 2200;
        private const double EPS = 1E-3;
        private const double BUST_STAMINA = 5;

        private const int MIN_GHOST_DIST_SQR = MIN_GHOST_DIST * MIN_GHOST_DIST;
        private const int MAX_GHOST_DIST_SQR = MAX_GHOST_DIST * MAX_GHOST_DIST;
        private const int RELEASE_DIST_SQR = RELEASE_DIST * RELEASE_DIST;
        private const int VISIBLE_RANGE_SQR = VISIBLE_RANGE * VISIBLE_RANGE;

        private static IList<Entity> _ghosts = new List<Entity>();
        private static IList<Entity> _prevStepGhosts = new List<Entity>();
        private static Point _myBasePoint = null;
        private static Point _oppBasePoint = null;
        private static int[,] _visibleCount = new int[9, 16];


        static void Main(string[] args)
        {

            int bustersPerPlayer = int.Parse(Console.ReadLine()); // the amount of busters you control
            int ghostCount = int.Parse(Console.ReadLine()); // the amount of ghosts on the map
            int myTeamId =
                int.Parse(Console
                    .ReadLine()); // if this is 0, your base is on the top left of the map, if it is one, on the bottom right
            var oppTeamId = myTeamId == 0 ? 1 : 0;

            _myBasePoint = myTeamId == 0 ? new Point(0, 0) : new Point(WIDTH - 1, HEIGHT - 1);
            _oppBasePoint = myTeamId == 0 ? new Point(WIDTH - 1, HEIGHT - 1) : new Point(0, 0);
            var addOppId = myTeamId == 0 ? 3 : 0;

            // game loop
            while (true)
            {
                Console.Error.WriteLine(bustersPerPlayer);
                Console.Error.WriteLine(ghostCount);
                Console.Error.WriteLine(myTeamId);

                if (_currStunDelay > 0) _currStunDelay--;

                var myBusters = new List<Entity>();
                var oppBusters = new List<Entity>();
                var ghosts = new List<Entity>();

                int entities = int.Parse(Console.ReadLine()); // the number of busters and ghosts visible to you
                Console.Error.WriteLine(entities);
                for (int i = 0; i < entities; i++)
                {
                    var str = Console.ReadLine();
                    Console.Error.WriteLine(str);

                    string[] inputs = str.Split(' ');

                    int entityId = int.Parse(inputs[0]); // buster id or ghost id
                    int x = int.Parse(inputs[1]);
                    int y = int.Parse(inputs[2]); // position of this buster / ghost
                    int entityType = int.Parse(inputs[3]); // the team id if it is a buster, -1 if it is a ghost.
                    int state = int.Parse(
                        inputs[4]); // For busters: 0=idle, 1=carrying a ghost. For ghosts: remaining stamina points.
                    int
                        value = int.Parse(
                            inputs[5]); // For busters: Ghost id being carried/busted or number of turns left when stunned. For ghosts: number of busters attempting to trap this ghost.

                    var entity = new Entity(entityId, new Point(x, y), state, value);
                    if (entityType == myTeamId)
                    {
                        myBusters.Add(entity);
                    }
                    else if (entityType == oppTeamId)
                    {
                        oppBusters.Add(entity);
                    }
                    else if (entityType == -1)
                    {
                        ghosts.Add(entity);
                    }
                }

                UpdateGhosts(myBusters, ghosts);
                UpdateVisibleCount(myBusters);

                var notStallingGhosts = new List<Entity>();
                Entity hunterBustingGhost = null;
                for (int i = 0; i < bustersPerPlayer; i++)
                {
                    var buster = myBusters[i];

                    if (i == 0)
                    {
                        //истощаем ВИДИМОГО призрака (или движемся к нему, если далеко)
                        hunterBustingGhost = ghosts
                            .Where(g => g.State > 0 && 
                                        MathHelper.GetSqrDist(g.Point, _myBasePoint) <
                                        MathHelper.GetSqrDist(g.Point, _oppBasePoint))
                            .OrderBy(g => GetBustTime(buster, g)).FirstOrDefault();

                        if (hunterBustingGhost != null && StartBustGhots(hunterBustingGhost, buster, myBusters[1]))
                        {
                            notStallingGhosts.Add(hunterBustingGhost);
                            var sqrDist = MathHelper.GetSqrDist(buster, hunterBustingGhost);
                            if (sqrDist >= MIN_GHOST_DIST_SQR &&
                                sqrDist <= MAX_GHOST_DIST_SQR)
                            {
                                Console.WriteLine($"BUST {hunterBustingGhost.Id}");
                            }
                            else
                            {
                                var movingPoint = GetBustTrapPointNew(buster, hunterBustingGhost, false);
                                Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y} {hunterBustingGhost.Id}");
                            }
                            continue;
                        }

                        //загоняем призраков
                        var stallGhost = GetStallingGhost(buster, notStallingGhosts, myBusters, oppBusters);
                        if (stallGhost != null)
                        {
                            notStallingGhosts.Add(stallGhost);
                            var stallPoint = GetStallPoint(stallGhost);
                            Console.WriteLine($"MOVE {stallPoint.X} {stallPoint.Y} GTB0");
                            continue;
                        }

                        MoveToMostFogPosition();

                    }
                    else if (i == 1)
                    {
                        if (buster.State == 1) //carrying a ghost
                        {
                            var baseSqrDist = MathHelper.GetSqrDist(buster.Point, _myBasePoint);
                            if (baseSqrDist <= RELEASE_DIST_SQR)
                            {
                                Console.WriteLine("RELEASE");
                                _caughtGhosts++;
                            }
                            else //move to base
                            {
                                Console.WriteLine($"MOVE {_myBasePoint.X} {_myBasePoint.Y}");
                            }
                            continue;
                        }

                        //trapping a ghost
                        var trapGhost = GetTrapGhost(buster, ghosts, true);
                        if (trapGhost != null)
                        {
                            notStallingGhosts.Add(trapGhost);
                            Console.WriteLine($"TRAP {trapGhost.Id}");
                            continue;
                        }
                       
                        //ловим свободных со стаминой 0
                        var zeroStaminaGhost = _ghosts.Where(g => g.State == 0)
                            .OrderBy(g => MathHelper.GetSqrDist(buster, g)).FirstOrDefault();
                        if (zeroStaminaGhost != null)
                        {
                            notStallingGhosts.Add(zeroStaminaGhost);
                            var movingPoint = GetBustTrapPointNew(buster, zeroStaminaGhost, false);
                            Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y}");
                            continue;
                        }

                        var bustingGhosts = ghosts.Where(IsBustingGhost);
                        Entity minTrapTimeGhost = null;
                        Point minTrapTimePoint = null;
                        var minTime = int.MaxValue;
                        foreach (var bg in bustingGhosts)
                        {
                            var bustingTime = IsDoubleBustingGhost(bg)
                                ? Convert.ToInt32(Math.Ceiling(bg.State / 2d))
                                : bg.State;

                            var trapPoint = GetBustTrapPointNew(buster, bg, true);
                            var trapPointDist = MathHelper.GetDist(buster.Point, trapPoint);
                            var trapTime = Convert.ToInt32(Math.Ceiling(trapPointDist / BUSTER_SPEED));

                            if (trapTime < bustingTime) continue;

                            if (trapTime < minTime)
                            {
                                minTime = trapTime;
                                minTrapTimePoint = trapPoint;
                                minTrapTimeGhost = bg;
                            }
                        }

                        if (minTrapTimeGhost != null) //идем ловить
                        {
                            notStallingGhosts.Add(minTrapTimeGhost);
                            Console.WriteLine($"MOVE {minTrapTimePoint.X} {minTrapTimePoint.Y}");
                            continue;
                        }

                        if (hunterBustingGhost != null)//идем ловить того, на кого охотится hunter
                        {
                            var bustingTime = hunterBustingGhost.State;

                            var trapPoint = GetBustTrapPointNew(buster, hunterBustingGhost, true);
                            var trapPointDist = MathHelper.GetDist(buster.Point, trapPoint);
                            var trapTime = Convert.ToInt32(Math.Ceiling(trapPointDist / BUSTER_SPEED));

                            if (bustingTime <= 2 || trapTime >= bustingTime)
                            {
                                notStallingGhosts.Add(hunterBustingGhost);
                                Console.WriteLine($"MOVE {trapPoint.X} {trapPoint.Y}");
                                continue;
                            }
                        }


                        var stallGhost = GetStallingGhost(buster, notStallingGhosts, myBusters, oppBusters);
                        if (stallGhost != null) //загоняем призраков
                        {
                            notStallingGhosts.Add(stallGhost);
                            var stallPoint = GetStallPoint(stallGhost);
                            Console.WriteLine($"MOVE {stallPoint.X} {stallPoint.Y} GTB1");
                            continue;
                        }

                        MoveToMostFogPosition();

                    }
                    else if (i == 2)
                    {
                        var stunOppBuster = oppBusters.SingleOrDefault(b =>
                            b.Id == 1 + addOppId && MathHelper.GetSqrDist(buster, b) <= MAX_GHOST_DIST_SQR);

                        if (_currStunDelay == 0 && stunOppBuster != null)
                        {
                            Console.WriteLine($"STUN {stunOppBuster.Id}");
                            _currStunDelay = STUN_DELAY;
                            continue;
                        }

                        var oppCatcher = oppBusters.SingleOrDefault(b => b.Id == 1 + addOppId);
                        if (!_isRadarUsed && !ghosts.Any() && _caughtGhosts >= 2 && oppCatcher != null && oppCatcher.State == 2)//TODO: bad if
                        {
                            Console.WriteLine("RADAR");
                            _isRadarUsed = true;
                            continue;
                        }
                        
                        if (oppCatcher != null) //move to opp catcher
                        {
                            Console.WriteLine($"MOVE {oppCatcher.Point.X} {oppCatcher.Point.Y} sd={_currStunDelay}");
                            continue;
                        }

                        var waitCatcherX = myTeamId == 0 ? WIDTH - VISIBLE_RANGE : VISIBLE_RANGE;
                        var waitCatcherY = myTeamId == 0 ? HEIGHT - VISIBLE_RANGE : VISIBLE_RANGE;
                        Console.WriteLine($"MOVE {waitCatcherX} {waitCatcherY} sd={_currStunDelay}");
                    }


                    // First: MOVE x y | BUST id
                    // Second: MOVE x y | TRAP id | RELEASE
                    // Third: MOVE x y | STUN id | RADAR
                }
            }
        }



        private static Entity GetStallingGhost(Entity buster, IList<Entity> notStallingGhosts, IList<Entity> myBuster, IList<Entity> oppBusters)
        {
            return _ghosts.Where(g => !notStallingGhosts.Contains(g) &&
                                      (!IsBustingGhost(g) ||
                                       (buster.Id == 0 || buster.Id == 3) && !oppBusters.Any(b =>
                                           b.State == 4 && MathHelper.GetSqrDist(b, g) >= MIN_GHOST_DIST_SQR &&
                                           MathHelper.GetSqrDist(b, g) <= MAX_GHOST_DIST_SQR)) &&
                                      !myBuster.Any(b => b.State == 2 && MathHelper.GetSqrDist(b, g) < EPS) &&
                                      !oppBusters.Any(b => b.State == 2 && MathHelper.GetSqrDist(b, g) < EPS) &&
                                      MathHelper.GetSqrDist(_myBasePoint, g.Point) > RELEASE_DIST_SQR)
                .OrderBy(g => MathHelper.GetSqrDist(buster, g)).FirstOrDefault();
        }

        private static bool StartBustGhots(Entity ghost, Entity hunter, Entity catcher)
        {
            if (catcher.State == 1) return false;
            var catchPoint = GetBustTrapPointNew(catcher, ghost, false);
            var dist = MathHelper.GetDist(catcher.Point, catchPoint);
            var catchTime = Convert.ToInt32(Math.Ceiling(dist / BUSTER_SPEED));
            if (catcher.State == 2) catchTime += catcher.Value;

            var bustTime = GetBustTime(hunter, ghost);
            return catchTime <= bustTime;
        }

        private static Point GetStallPoint(Entity ghost)
        {
            var vector = new Vector(_myBasePoint, ghost.Point);
            var coeff = (vector.Length + 800d) / vector.Length;
            var multVector = MathHelper.GetMultVector(vector, coeff);
            return multVector.End;
        }

        private static void MoveToMostFogPosition()
        {
            //TODO!!! Move clever!

            var minVisCount = int.MaxValue;
            var minJ = -1;
            var minI = -1;
            for (var i = 0; i < 9; ++i)
                for (var j = 0; j < 16; ++j)
                    if (_visibleCount[i, j] < minVisCount)
                    {
                        minVisCount = _visibleCount[i, j];
                        minJ = j;
                        minI = i;
                    }
           
            Console.WriteLine($"MOVE {minJ * 1000} {minI * 1000}");
        }

        private static Entity GetTrapGhost(Entity buster, IList<Entity> ghosts, bool considerDist)
        {
            return ghosts.FirstOrDefault(g => g.State == 0 && (!considerDist ||
                                                               MathHelper.GetSqrDist(buster, g) >= MIN_GHOST_DIST_SQR &&
                                                               MathHelper.GetSqrDist(buster, g) <= MAX_GHOST_DIST_SQR));
        }


        private static Entity GetBustGhost(Entity buster, IList<Entity> ghosts)
        {
            var okDistGhosts = ghosts.Where(g => g.State > 0 &&
                                                 MathHelper.GetSqrDist(buster, g) >= MIN_GHOST_DIST_SQR &&
                                                 MathHelper.GetSqrDist(buster, g) <= MAX_GHOST_DIST_SQR);

            var orderedOkDistGhosts = okDistGhosts.OrderBy(g => g.State).ThenBy(g => MathHelper.GetSqrDist(buster, g));
            return orderedOkDistGhosts.FirstOrDefault();
        }

        private static Point GetBustTrapPointNew(Entity buster, Entity ghost, bool isStaticGhost)
        {
            var dist = MathHelper.GetDist(ghost, buster);
           
            if (dist >= MIN_GHOST_DIST && dist <= MAX_GHOST_DIST)
            {
                return buster.Point;
            }
            else if (dist < MIN_GHOST_DIST)
            {
                var vector = new Vector(ghost.Point, _myBasePoint);
                if (Math.Abs(vector.Length) < EPS)
                    vector.End = new Point(WIDTH / 2, HEIGHT / 2);

                var minVectorCoeff = MIN_GHOST_DIST * 1d / vector.Length;
                var minVector = MathHelper.GetMultVector(vector, minVectorCoeff);

                var maxVectorCoeff = MAX_GHOST_DIST * 1d / vector.Length;
                var maxVector = MathHelper.GetMultVector(vector, maxVectorCoeff);

                var movingPoint = MathHelper.GetMiddlePoint(minVector.End, maxVector.End);
                return movingPoint;
            }
            else //dist > MAX_GHOST_DIST
            {
                if (isStaticGhost)
                {
                    var vector = new Vector(ghost.Point, buster.Point);

                    var minVectorCoeff = MIN_GHOST_DIST * 1d / vector.Length;
                    var minVector = MathHelper.GetMultVector(vector, minVectorCoeff);

                    var maxVectorCoeff = MAX_GHOST_DIST * 1d / vector.Length;
                    var maxVector = MathHelper.GetMultVector(vector, maxVectorCoeff);

                    var movingPoint = MathHelper.GetMiddlePoint(minVector.End, maxVector.End);
                    return movingPoint;
                }
                else
                {
                    var busterDist = 0d;
                    var ghostDist = 0d;
                    while (dist - busterDist + ghostDist> MAX_GHOST_DIST)
                    {
                        if (dist - busterDist + ghostDist <= GHOST_VISIBLE_RANGE)
                        {
                            ghostDist += GHOST_SPEED;
                        }
                        busterDist += BUSTER_SPEED;
                    }

                    var vector = new Vector(buster.Point, ghost.Point);
                    var ghostCoeff = (vector.Length + ghostDist) / vector.Length;
                    var ghostMultVector = MathHelper.GetMultVector(vector, ghostCoeff);
                    var finalGhostPoint = ghostMultVector.End;

                    var busterCoeff = busterDist / vector.Length;
                    var busterMultVector = MathHelper.GetMultVector(vector, busterCoeff);
                    var finalBusterPoint = busterMultVector.End;

                    return finalBusterPoint;
                }
            }

        }

        private static Point GetBustTrapPoint(Entity buster, Entity ghost)
        {
            var sqrDist = MathHelper.GetSqrDist(buster, ghost);
            if (sqrDist > MAX_GHOST_DIST_SQR) return ghost.Point;

            var vector = new Vector(ghost.Point, buster.Point);
            if (Math.Abs(vector.Length) < EPS)
                vector.End = _myBasePoint;
            if (Math.Abs(vector.Length) < EPS)
                vector.End = new Point(WIDTH / 2, HEIGHT / 2);


            var minVectorCoeff = MIN_GHOST_DIST * 1d / vector.Length;
            var minVector = MathHelper.GetMultVector(vector, minVectorCoeff);

            var maxVectorCoeff = MAX_GHOST_DIST * 1d / vector.Length;
            var maxVector = MathHelper.GetMultVector(vector, maxVectorCoeff);

            var movingPoint = MathHelper.GetMiddlePoint(minVector.End, maxVector.End);
            return movingPoint;
        }

        private static void UpdateVisibleCount(IList<Entity> myBusters)
        {
            foreach (var buster in myBusters)
                for (var i = 0; i < 9; ++i)
                    for (var j = 0; j < 16; ++j)
                    {
                        var y = 1000 * i;
                        var x = 1000 * j;
                        if (MathHelper.GetSqrDist(buster, x, y) <= VISIBLE_RANGE_SQR)
                            _visibleCount[i, j]++;
                    }
        }

        private static void UpdateGhosts(IList<Entity> myBusters, IList<Entity> ghosts)
        {
            _prevStepGhosts = _ghosts.ToList();

            //Remove
            for (var i = _ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = _ghosts[i];
                if (ghosts.Any(g => g.Id == ghost.Id)) continue;

                var isVisible = false;
                foreach (var buster in myBusters)
                {
                    var dist = MathHelper.GetSqrDist(buster, ghost);
                    if (dist <= (VISIBLE_RANGE - GHOST_SPEED) * (VISIBLE_RANGE - GHOST_SPEED))
                    {
                        isVisible = true;
                        break;
                    }
                }

                if (isVisible) _ghosts.RemoveAt(i);
            }

            //Update and Add
            foreach (var ghost in ghosts)
            {
                var index = -1;
                for (var i = 0; i < _ghosts.Count; ++i)
                {
                    if (_ghosts[i].Id == ghost.Id)
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    _ghosts[index] = ghost;
                }
                else
                {
                    _ghosts.Add(ghost);
                }

            }
        }

        private static int GetBustTime(Entity buster, Entity ghost)
        {
            int time = 0;
            var sqrDistFromBuster = MathHelper.GetSqrDist(buster, ghost);
            if (sqrDistFromBuster < MIN_GHOST_DIST_SQR || sqrDistFromBuster > MAX_GHOST_DIST_SQR)
            {
                var bustPoint = GetBustTrapPointNew(buster, ghost, false);
                var dist = MathHelper.GetDist(buster.Point, bustPoint);
                time += Convert.ToInt32(Math.Ceiling(dist / BUSTER_SPEED));
            }
            time += ghost.State;
            return time;
        }


        private static bool IsBustingGhost(Entity ghost)
        {
            var prevG = _prevStepGhosts.SingleOrDefault(g => g.Id == ghost.Id);
            return prevG != null && prevG.State - ghost.State >= 1;
        }

        private static bool IsDoubleBustingGhost(Entity ghost)
        {
            var prevG = _prevStepGhosts.SingleOrDefault(g => g.Id == ghost.Id);
            return prevG != null && prevG.State - ghost.State == 2;
        }



        class Point
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        class Vector
        {
            public Point Start { get; set; }
            public Point End { get; set; }

            public Vector(Point start, Point end)
            {
                Start = start;
                End = end;
            }

            public double Length => MathHelper.GetDist(Start, End);
        }

        class Entity
        {
            public int Id { get; set; }
            public Point Point { get; set; }
            public int State { get; set; }
            public int Value { get; set; }

            public Entity(int id, Point point, int state, int value)
            {
                Id = id;
                Point = point;
                State = state;
                Value = value;
            }
        }

        static class MathHelper
        {
            public static int GetSqrDist(Entity e, int x, int y)
            {
                return (e.Point.X - x) * (e.Point.X - x) + (e.Point.Y - y) * (e.Point.Y - y);
            }

            public static int GetSqrDist(Entity e1, Entity e2)
            {
                return GetSqrDist(e1.Point, e2.Point);
            }

            public static int GetSqrDist(Point p1, Point p2)
            {
                return (p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y);
            }

            public static double GetDist(Point p1, Point p2)
            {
                return Math.Sqrt(GetSqrDist(p1, p2));
            }

            public static double GetDist(Entity e1, Entity e2)
            {
                return GetDist(e1.Point, e2.Point);
            }

            public static Vector GetMultVector(Vector v, double coeff)
            {
                var endX = v.Start.X + (v.End.X - v.Start.X) * coeff;
                var endY = v.Start.Y + (v.End.Y - v.Start.Y) * coeff;
                return new Vector(v.Start, new Point(Convert.ToInt32(endX), Convert.ToInt32(endY)));
            }

            public static Point GetMiddlePoint(Point p1, Point p2)
            {
                var x = (p1.X + p2.X) / 2;
                var y = (p1.Y + p2.Y) / 2;
                return new Point(x, y);
            }
        }
    }
}
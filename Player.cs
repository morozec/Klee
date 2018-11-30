using System;
using System.Collections.Generic;
using System.Linq;

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
        private const int VISIBLE_DIST = 2200;
        private const int BUSTER_SPEED = 800;
        private const int GHOST_SPEED = 400;
        private const double EPS = 1E-3;

        private const int MIN_GHOST_DIST_SQR = MIN_GHOST_DIST * MIN_GHOST_DIST;
        private const int MAX_GHOST_DIST_SQR = MAX_GHOST_DIST * MAX_GHOST_DIST;
        private const int RELEASE_DIST_SQR = RELEASE_DIST * RELEASE_DIST;

        private static IList<Entity> _ghosts = new List<Entity>();

        static void Main(string[] args)
        {

            int bustersPerPlayer = int.Parse(Console.ReadLine()); // the amount of busters you control
            int ghostCount = int.Parse(Console.ReadLine()); // the amount of ghosts on the map
            int myTeamId =
                int.Parse(Console
                    .ReadLine()); // if this is 0, your base is on the top left of the map, if it is one, on the bottom right
            var oppTeamId = myTeamId == 0 ? 1 : 0;

            var basePoint = myTeamId == 0 ? new Point(0, 0) : new Point(WIDTH - 1, HEIGHT - 1);
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

                Entity bustGhost = null;
                for (int i = 0; i < bustersPerPlayer; i++)
                {
                    var buster = myBusters[i];

                    if (i == 0)
                    {
                        /*
                        bustGhost = GetBustGhost(buster, ghosts);
                        if (bustGhost != null) //got ghost to bust
                        {
                            Console.WriteLine($"BUST {bustGhost.Id}");
                            continue;
                        }
                        */


                        /*                       
                       bustGhost = ghosts.Where(g => g.State > 0).OrderBy(g => MathHelper.GetSqrDist(buster, g)).FirstOrDefault();
                       if (bustGhost != null)//move to bust point
                       {
                           var movingPoint = GetBustTrapPoint(buster, bustGhost, basePoint);
                           Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y}");
                           continue;
                       }
                       */


                        bustGhost = _ghosts.Where(g => g.State > 0).OrderBy(g => GetBustTime(buster, g, basePoint)).FirstOrDefault();
                        if (bustGhost != null)
                        {
                            if (MathHelper.GetSqrDist(buster, bustGhost) >= MIN_GHOST_DIST_SQR &&
                                MathHelper.GetSqrDist(buster, bustGhost) <= MAX_GHOST_DIST_SQR)
                            {
                                Console.WriteLine($"BUST {bustGhost.Id}");
                            }
                            else
                            {
                                var movingPoint = GetBustTrapPoint(buster, bustGhost, basePoint);
                                Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y}");
                            }
                            continue;
                        }

                        var moveToBaseGhost = _ghosts.Where(g => g.State == 0 && MathHelper.GetSqrDist(basePoint, g.Point) > RELEASE_DIST_SQR)
                            .OrderBy(g => MathHelper.GetSqrDist(buster, g)).FirstOrDefault();
                        if (moveToBaseGhost != null)
                        {
                            var vector = new Vector(basePoint, moveToBaseGhost.Point);
                            var coeff = (vector.Length + 100d) / vector.Length;
                            var multVector = MathHelper.GetMultVector(vector, coeff);
                            var movingPoint = multVector.End;

                            Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y} GTB");
                            continue;
                        }

                        MoveToRandomPosition();

                    }
                    else if (i == 1)
                    {
                        if (buster.State == 1) //carrying a ghost
                        {
                            var baseSqrDist = MathHelper.GetSqrDist(buster.Point, basePoint);
                            if (baseSqrDist <= RELEASE_DIST_SQR)
                            {
                                Console.WriteLine("RELEASE");
                                _caughtGhosts++;
                            }
                            else //move to base
                            {
                                Console.WriteLine($"MOVE {basePoint.X} {basePoint.Y}");
                            }
                            continue;
                        }

                        var trapGhost = GetTrapGhost(buster, ghosts, true);
                        if (trapGhost != null)
                        {
                            Console.WriteLine($"TRAP {trapGhost.Id}");
                            continue;
                        }

                        //trapGhost = GetTrapGhost(buster, ghosts, false);
                        //if (trapGhost != null)
                        //{
                        //    var movingPoint = GetBustTrapPoint(buster, bustGhost, basePoint);
                        //    Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y}");
                        //    continue;
                        //}

                        var zeroStaminaGhost = _ghosts.Where(g => g.State == 0)
                            .OrderBy(g => MathHelper.GetSqrDist(buster, g)).FirstOrDefault();
                        if (zeroStaminaGhost != null)
                        {
                            var movingPoint = GetBustTrapPoint(buster, zeroStaminaGhost, basePoint);
                            Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y}");
                            continue;
                        }

                        // move to trap point
                        if (bustGhost != null)
                        {
                            var movingPoint = GetBustTrapPoint(buster, bustGhost, basePoint);
                            Console.WriteLine($"MOVE {movingPoint.X} {movingPoint.Y}");
                            continue;
                        }

                        MoveToRandomPosition();

                    }
                    else if (i == 2)
                    {
                        var stunOppBuster = oppBusters.SingleOrDefault(b =>
                            (b.State == 1 || b.State == 3) && MathHelper.GetSqrDist(buster, b) <= MAX_GHOST_DIST_SQR);

                        if (stunOppBuster == null)
                        {
                            stunOppBuster = oppBusters.SingleOrDefault(b =>
                                b.State == 4 && MathHelper.GetSqrDist(buster, b) <= MAX_GHOST_DIST_SQR);
                        }

                        if (_currStunDelay == 0 && stunOppBuster != null)
                        {
                            Console.WriteLine($"STUN {stunOppBuster.Id}");
                            _currStunDelay = STUN_DELAY + 1;
                            continue;
                        }

                        if (!_isRadarUsed && !ghosts.Any() && _caughtGhosts >= 2)//TODO: bad if
                        {
                            Console.WriteLine("RADAR");
                            _isRadarUsed = true;
                            continue;
                        }


                        //move to opp catcher
                        var oppCatcher = oppBusters.SingleOrDefault(b => b.Id == 1 + addOppId);
                        if (oppCatcher != null)
                        {
                            Console.WriteLine($"MOVE {oppCatcher.Point.X} {oppCatcher.Point.Y}");
                            continue;
                        }

                        //move to opp hunter
                        var oppHunter = oppBusters.SingleOrDefault(b => b.Id == 0 + addOppId);
                        if (oppHunter != null)
                        {
                            Console.WriteLine($"MOVE {oppHunter.Point.X} {oppHunter.Point.Y}");
                            continue;
                        }

                        var oppSupport = oppBusters.SingleOrDefault(b => b.Id == 2 + addOppId);

                        MoveToRandomPosition();
                    }


                    // First: MOVE x y | BUST id
                    // Second: MOVE x y | TRAP id | RELEASE
                    // Third: MOVE x y | STUN id | RADAR
                }
            }
        }

        private static void MoveToRandomPosition()
        {
            var x = _rnd.Next(WIDTH);
            var y = _rnd.Next(HEIGHT);
            Console.WriteLine($"MOVE {x} {y}");
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

        private static Point GetBustTrapPoint(Entity buster, Entity ghost, Point basePoint)
        {
            var vector = new Vector(ghost.Point, buster.Point);
            if (Math.Abs(vector.Length) < EPS)
                vector.End = basePoint;
            if (Math.Abs(vector.Length) < EPS)
                vector.End = new Point(WIDTH / 2, HEIGHT / 2);


            var minVectorCoeff = MIN_GHOST_DIST * 1d / vector.Length;
            var minVector = MathHelper.GetMultVector(vector, minVectorCoeff);

            var maxVectorCoeff = MAX_GHOST_DIST * 1d / vector.Length;
            var maxVector = MathHelper.GetMultVector(vector, maxVectorCoeff);

            var movingPoint = MathHelper.GetMiddlePoint(minVector.End, maxVector.End);
            return movingPoint;
        }

        private static void UpdateGhosts(IList<Entity> myBusters, IList<Entity> ghosts)
        {
            //Remove
            for (var i = _ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = _ghosts[i];
                if (ghosts.Any(g => g.Id == ghost.Id)) continue;

                var isVisible = false;
                foreach (var buster in myBusters)
                {
                    var dist = MathHelper.GetSqrDist(buster, ghost);
                    if (dist <= (VISIBLE_DIST - GHOST_SPEED) * (VISIBLE_DIST - GHOST_SPEED))
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

        private static int GetBustTime(Entity buster, Entity ghost, Point basePoint)
        {
            var bustPoint = GetBustTrapPoint(buster, ghost, basePoint);
            var dist = MathHelper.GetDist(buster.Point, bustPoint);
            var time = Convert.ToInt32(dist / BUSTER_SPEED);
            time += ghost.State;
            return time;
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
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace quadTree
{
    class Program
    {
        static void Main(string[] args)
        {
            QuadTree tree = new QuadTree();
            Random ran = new Random((int)DateTime.Now.Ticks);

            // 长宽2000
            int hl=1000;
            tree.Init(new MBR(-hl, hl, hl, -hl));
            var qd1 = (new QuadData()
            {
                Id = 1,
                mbr = new MBR(1, 20, 30, 1),
                Next = null,
            });
            var qd2 = (new QuadData()
            {
                Id = 2,
                mbr = new MBR(-30, -1, 10, 1),
                Next = null,
            });
            var qd3 = new QuadData()
            {
                Id = 3,
                mbr = new MBR(-30, -1, -1, -10),
                Next = null,
            };
            var qd4 = new QuadData()
            {
                Id = 4,
                mbr = new MBR(1, 20, -1, -40),
                Next = null,
            };

            // 1,2,3,4象限个插入一个
            tree.Add(qd1);
            tree.Add(qd2);
            tree.Add(qd3);
            tree.Add(qd4);

            //  随机插入100个
            int insertCnt = 5000;
            float x, z;
            int minwidth=1,maxwidth=20, minlen=1,maxlen=30;
            List<QuadData> rmList = new List<QuadData>();
            for (int i= 0;i < insertCnt;++i)
            {
                var qd = new QuadData();
                qd.Id = i;
                float l, r, t, b;
                // 坐标
                x = ran.Next(-hl, hl);
                z = ran.Next(-hl, hl);
                //
                float hwidth = ran.Next(minwidth, maxwidth);
                float hlen= ran.Next(minlen, maxlen);
                //
                l = x - hwidth;
                r = x + hwidth;
                t = z + hlen;
                b = z - hlen;
                qd.mbr = new MBR(l, r, t, b);
                qd.Next = null;
                tree.Add(qd);
                rmList.Add(qd);
            }

            string info = "";
            foreach (var levelData in tree.LevelCnt)
                info += $"({levelData.Key}:{levelData.Value})-";

            Console.WriteLine($"tree level:{tree.Current_Max_Level} {info}");

            // 
            var circle = new Circle()
            {
                 x = 0,
                 z = 0,
                 radius = 50,
            };
            Stopwatch sw = new Stopwatch();
            sw.Start();


            // 每秒一次搜索, 这些单位平均一秒执行搜索次数
            int searchCnt = 1;// insertCnt;
            for(int i= 0;i<searchCnt;++i)
            {
                var lst = tree.Find(circle);
                Console.WriteLine($"find {i} lst:{lst.Count} treeTest:{tree.CorssCircleTestCnt}");
            }

            var ms = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"find ms={ms} from cnt:{insertCnt} treeTest:{tree.CorssCircleTestCnt}");

            sw.Restart();
            int rmCnt = 0;
            bool rm = tree.Remove(qd1);
            if (rm)
                rmCnt++;
            ms = sw.Elapsed.TotalMilliseconds;
            for (int i = 0; i < rmList.Count; ++i)
            {
                rm = tree.Remove(rmList[i]);
                if (rm)
                    rmCnt++;
                if (false == rm)
                    Console.WriteLine($"rm rmList[i] {rmList[i].Id} not success");
            }
            Console.WriteLine($"remove ms={ms} rm:{rm} rmCnt:{rmCnt}");

            sw.Restart();
            tree.Remove(qd2);
            tree.Clear();
            tree.Remove(qd3);
            ms = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"clear ms={ms}");
            Console.WriteLine("Hello World!");
        }
    }
}

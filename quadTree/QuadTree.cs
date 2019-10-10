using System;
using System.Collections.Generic;
using System.Text;

namespace quadTree
{
    // 象限
    /*
                  |
                  |
            UL    |    UR
        ---------------------
                  |
            LL    |    LR
                  |
                  |
    */
    // 象限枚举
   public enum EQuadrant
    {
        UR, // 1
        UL, // 2
        LL, // 3
        LR, // 4

        MAX,
        ROOT = 901124,
    }
    // 最小2d包围盒
   public class MBR
    {
        public MBR(float l, float r, float t, float b)
        {
            // 保证r>l t>b
            if(l>r)
            {
                float tmp = l;
                l = r;
                r = tmp;
            }
            if(b>t)
            {
                float tmp = b;
                b = t;
                t = tmp;
            }
            left = l;
            right = r;
            top = t;
            bottom = b;
        }
        public float left;
        public float right;
        public float top;
        public float bottom;
    }

    // 圆形碰撞检测数据
    public class Circle
    {
        public float x;
        public float z;
        public float radius;
    }

    // 节点存放实体的数据
   public class QuadData
    {
        public QuadData Next;
        public MBR mbr;
        public int Id; // 实体Id
    }

    // 四叉树节点
    public class QuadNode
    {
        // 节点最小包围盒
        public MBR mbr;
        // 节点Id
        public int Id;
        // 实体个数
        public int Cnt;
        // 子象限，如果cnt>MAX_NODE_CNT 分裂
        public QuadNode[] Sub;
        // 节点存放的实体, 单位存在于多象限中
        public QuadData Child;

        // 所处象限
        public EQuadrant Quadrant;

        // 所在层
        public int Level;
    }

    // 四叉树
    public  class QuadTree
    {
        // debug
        public Dictionary<int, int> LevelCnt; // 每层多少单位
        public int TotalCnt;
        // find 方法调用碰撞检测次数
        public int CorssCircleTestCnt = 0;
        public int Current_Max_Level = 0;    // 层数
        // debug

        public const int MAX_DATA_CNT = 20;  // 节点分裂单位数
        public const int MAX_QUADRANT = 4;   // 象限数

        // 四叉树根节点
        private QuadNode _root;

        // 初始化四叉树范围
        public void Init(MBR mbr)
        {
            if (null != _root)
                return;
            _root = new QuadNode();
            _root.mbr = mbr;
            _root.Quadrant = EQuadrant.ROOT;
            _root.Level = 0;

            // debug
            LevelCnt = new Dictionary<int, int>();
            TotalCnt = 0;
            CorssCircleTestCnt = 0;
            // debug
        }

        // 添加
        public bool Add(QuadData data)
        {
            // 节点node数量判定
            // 寻找符合节点插入
            bool success = false;
            QuadNode tmp = _root;
            do
            {
                bool addToParentNode = false;
                // 当前节点child个数小于最大child个数,并且没有分裂过
                if (tmp.Cnt + 1 <= MAX_DATA_CNT && null==tmp.Sub)
                    addToParentNode = true;

                // 节点个数<MAX_DATA_CNT && 为分裂的
                // 多象限内直接插入本节点
                if(addToParentNode || isInMultipleSub(tmp,data))
                {
                    // 加入本节点
                    var n1 = tmp.Child;
                    tmp.Child = data;
                    data.Next = n1;
                    tmp.Cnt++;
                    TotalCnt++;
                    addLevelCnt(tmp.Level);
                    success = true;
                    // 跳出
                    break;
                }
                else
                {
                    split(tmp);
                    // 获取插入的指定象限，
                    tmp = GetSubNode(tmp, data);
                }
            }while (true);

            return success;
        }
        public QuadNode GetSubNode(QuadNode node,QuadData data)
        {
            QuadNode sub = null;
            for (int i = 0; i < MAX_QUADRANT; ++i)
            {
                if (false == isRegionCross(node.Sub[i].mbr, data.mbr))
                    continue;

                sub = node.Sub[i];
                break;
            }

            return sub;
        }
        // 更新
        // 跟新data在四叉树中的位置
        public bool Update(QuadData data, QuadData newData)
        {
            bool success = Remove(data);
            if (false == success)
            {
                // 失败了，逻辑错误
                log($"error remove data. {data.Id}");
                return false;
            }

            Add(newData);
            return true;
        }

        // 清空tree
        public void Clear()
        {
            Queue<QuadNode> nodes = new Queue<QuadNode>();
            nodes.Enqueue(_root);
            var tmp = _root;
            do
            {
                tmp = nodes.Dequeue();
                var n1 = tmp.Child;
                tmp.Child = null;
                while (null != n1)
                {
                    TotalCnt--;
                    LevelCnt[tmp.Level]--;
                    var n1Tmp = n1.Next;
                    n1.Next = null;
                    n1 = n1Tmp;
                }

                // 象限遍历检测
                if (null != tmp.Sub)
                {
                    for (int i = 0; i < MAX_QUADRANT; ++i)
                    {
                        // 缓存象限数据
                        nodes.Enqueue(tmp.Sub[i]);

                        // 清理象限引用
                        // tmp.Sub[i] = null;
                    }
                }
            }
            while (nodes.Count > 0);
        }

        // 删除
        // 从四叉树中移除,调用时需要data数据修改前调用，如果data数据修改后调用，可能无法找到。
        // 导致数据错误
        public bool Remove(QuadData data)
        {
            var tmp = _root;
            bool rm = false;
            bool success = false;

            // 没有节点
            do
            {
                // 节点数 满足，或者处于多个象限中
                if ((tmp.Cnt <= MAX_DATA_CNT && null == tmp.Sub) || isInMultipleSub(tmp,data))
                {
                    // 跳出循环
                    rm = true;

                    // 重复删除同一个数据
                    if (null == tmp.Child)
                        break;

                    // 本节点查找
                    var curNode = tmp.Child;
                    var preNode = tmp.Child;

                    do
                    {
                        if (curNode == data)
                        {
                            // 头
                            if (curNode == tmp.Child)
                                tmp.Child = curNode.Next; 
                            else 
                                preNode.Next = curNode.Next;

                            success = true;
                            tmp.Cnt--;
                            TotalCnt--;
                            LevelCnt[tmp.Level]--;
                            break;
                        }

                        preNode = curNode;
                        curNode = curNode.Next;

                    } while (null != curNode);
                }
                else
                {
                    // 获取所在象限
                    tmp = GetSubNode(tmp, data);
                }
            } while (false == rm);

            return success;
        }
        // 查找radius范围内的单位 ,圆形查找
        // 优化，如果node节点全在circle内，无需再判定相交
        // 直接遍历node节点和子节点即可
        public List<QuadData> Find(Circle circle)
        {
            List<QuadData> list = new List<QuadData>();
            var tmp = _root;
            Queue<QuadNode> nodes = new Queue<QuadNode>();
            nodes.Enqueue(_root);
            do
            {
                tmp = nodes.Dequeue();
                // 如果相交
                if (isCrossCricle(tmp.mbr, circle))
                {

                    // 当前节点存放的单位检测
                    var n1 = tmp.Child;
                    while (null != n1)
                    {
                        if (isCrossCricle(n1.mbr, circle))
                            list.Add(n1);
                        n1 = n1.Next;
                    }

                    // 象限遍历检测
                    if(null != tmp.Sub)
                    {
                        for (int i = 0; i < MAX_QUADRANT; ++i)
                        {
                            // 缓存象限数据
                            nodes.Enqueue(tmp.Sub[i]);
                        }
                    }
                }
            } while (nodes.Count > 0);
            return list;
        }
        // 判断mbr和圆是否相交
        private bool isCrossCricle(MBR mbr, Circle circle)
        {
            CorssCircleTestCnt++;
            // 1. 矩形的四点判断 点在圆内
            // 2. 矩形的四边判断 边与圆相交
            // [算法](https://www.cnblogs.com/llkey/p/3707351.html)

            // 01 点在圆内
            if (isPointInCricle(mbr.left, mbr.top, circle))
                return true;
            else if (isPointInCricle(mbr.right, mbr.top, circle))
                return true;
            else if (isPointInCricle(mbr.right, mbr.bottom, circle))
                return true;
            else if (isPointInCricle(mbr.left, mbr.bottom, circle))
                return  true;

            // 02 边与圆相交
            float rec_x = (mbr.right - mbr.left) * 0.5f;
            float rec_z = (mbr.top - mbr.bottom) * 0.5f;
            float deltaX = Math.Abs(rec_x - circle.x);
            float deltaZ = Math.Abs(rec_z - circle.z);
            if (deltaX * deltaX + deltaZ * deltaZ <= circle.radius * circle.radius)
                return true;

            // 03 圆点在矩形内
            if (isPointInRegion(circle.x, circle.z, mbr))
                return true;


            return false;
        }
        private bool isPointInCricle(float x, float z, Circle circle)
        {
            float deltaX = Math.Abs(x - circle.x);
            float deltaZ = Math.Abs(z - circle.z);
            if (deltaX * deltaX + deltaZ * deltaZ > circle.radius * circle.radius)
                return false;
            return true;
        }
        private bool isPointInRegion(float x, float z, MBR mbr)
        {
            //
            if (x < mbr.left || x > mbr.right || z > mbr.top || z < mbr.bottom)
                return false;
            return true;
        }
        // 区域相交
        private bool isRegionCross(MBR m1, MBR m2)
        {
            /*
             * [矩形相交](https://blog.csdn.net/qq_40482358/article/details/86537747)
             cx1=max(ax1,bx1)         //左下x为最大的左下x集合
             cy1=max(ay1,by1)         //左下y为最大的左下y集合
             cx2=min(ax2,bx2)         //右上x为最小的右上x集合
             cy2=min(ay2,by2)         //右上y为最小的右上y集合
             矩形相交后仍然是矩形
             */
            float cx1 = Math.Max(m1.left, m2.left);
            float cy1 = Math.Max(m1.bottom, m2.bottom);
            float cx2 = Math.Min(m1.right, m2.right);
            float cy2 = Math.Min(m1.top, m2.top);

            if (cx1 > cx2) return false;
            if (cy1 > cy2) return false;

            return true;
        }

        // data是否在多个象限内
        // 多象限内，放到父节点上
        private bool isInMultipleSub(QuadNode node, QuadData data)
        {
            int cnt = 0;
            if (null == node.Sub)
                split(node);

            for (int i = 0; i < 4; ++i)
            {
                if (isRegionCross(node.Sub[i].mbr, data.mbr))
                {
                    cnt++;
                }
            }

            if (0 == cnt) // 大于整个象限了
                return true;
            else
            if (cnt > 1)   // 处于多个象限
                return true;
            else
                return false;
        }

        // 象限分裂
        // 分裂后，node节点的数据，尝试插入到子象限内
        private void split(QuadNode node)
        {
            // 已经分裂过了
            if (null != node.Sub)
                return;
            // 分裂为四个象限
            /*
                          |
                          |
                    UL    |    UR
                ---------------------
                          |
                    LL    |    LR
                          |
                          |
            */
            // mbr = (x,z,w,l)
            var mbr = node.mbr;
            float hw = (mbr.right - mbr.left) * 0.5f;
            float hl = (mbr.top - mbr.bottom) * 0.5f;
            var mbr_ur = new MBR(mbr.right-hw, mbr.right, mbr.top, mbr.top-hl);
            var mbr_ul = new MBR(mbr.left,mbr.left+hw,mbr.top,mbr.top-hl);
            var mbr_ll = new MBR(mbr.left,mbr.left+hw,mbr.bottom+hl,mbr.bottom);
            var mbr_lr = new MBR(mbr.right-hw, mbr.right, mbr.bottom+hl,mbr.bottom);

            node.Sub = new QuadNode[4];
            node.Sub[0] = new QuadNode() { Level = node.Level + 1, Quadrant = EQuadrant.UR,Cnt = 0,mbr = mbr_ur,Child=null,Sub=null,};
            node.Sub[1] = new QuadNode() { Level = node.Level + 1, Quadrant = EQuadrant.UL,Cnt = 0,mbr = mbr_ul,Child=null,Sub=null,};
            node.Sub[2] = new QuadNode() { Level = node.Level + 1, Quadrant = EQuadrant.LL,Cnt = 0,mbr = mbr_ll,Child=null,Sub=null,};
            node.Sub[3] = new QuadNode() { Level = node.Level + 1, Quadrant = EQuadrant.LR,Cnt = 0,mbr = mbr_lr,Child=null,Sub=null,};

            if (node.Level + 1 > Current_Max_Level)
                Current_Max_Level = node.Level + 1;

            // 尝试插入当前节点的child
            TotalCnt -= node.Cnt;
            LevelCnt[node.Level] -= node.Cnt;
            node.Cnt = 0;
            if(node.Child != null)
            {
                var tmp = node.Child;
                var tmp1 = tmp.Next;
                node.Child = null;
                while (null != tmp)
                {
                    Add(tmp);
                    tmp = tmp1;
                    if (null != tmp)
                    {
                        tmp1 = tmp.Next;
                        tmp.Next = null;
                    }
                }
            }
        }

        private void addLevelCnt(int level)
        {
            if (false == LevelCnt.ContainsKey(level))
                LevelCnt.Add(level, 0);

            LevelCnt[level] += 1;
        }

        private void log(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}

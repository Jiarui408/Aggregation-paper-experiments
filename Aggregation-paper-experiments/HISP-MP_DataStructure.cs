using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregation_paper_experiments
{
	class HISP_MP_DataStructure
	{
		//信息存储
		public Dictionary<int, Vacancy> vacancies = new Dictionary<int, Vacancy>();
		public Dictionary<int, Interval> intervals = new Dictionary<int, Interval>();
		public Dictionary<int, List<int>> vacancy_intervalnames = new Dictionary<int, List<int>>();
		public Dictionary<string, Arc> DAG_arcs = new Dictionary<string, Arc>();
		public Dictionary<string, Node> DAG_nodes = new Dictionary<string, Node>();
		public List<int> axis = new List<int>();
		public Dictionary<int, Aggregate_ISP_Flow> aggre_flows = new Dictionary<int, Aggregate_ISP_Flow>();
		public Dictionary<int, Dictionary<string, int>> aggregateflow_arc_quanity = new Dictionary<int, Dictionary<string, int>>();

		public Random rd = new Random(1);
		public int time_max = 1440;
		public int max_profit = 1000;

		//Gurobi设置
		public int time = 3600;
		public double gap = 0.0;

		//输出数据
		public double aggregation_solving_time = 0.0;
		public double disaggregation_solving_time = 0.0;
		public double original_solving_time = 0.0;
		public double aggregation_gap = 100.0;
		public double original_gap = 100.0;

		/// <summary>
		/// 便于一次求解使用的读取数据方法
		/// </summary>
		/// <param name="iternum"></param>
		public void ReadData(int iternum)
		{
			HashSet<int> axis_temp = new HashSet<int>();
			ReadIntervals();
			ReadVacancies();

			void ReadIntervals()
			{
				//读取              
				FileStream fs = new FileStream("HISP-MP_instances\\" + "intervals" + iternum + ".csv", FileMode.Open);
				StreamReader sr = new StreamReader(fs);
				string NoteData = sr.ReadLine();
				while (NoteData != null && NoteData != "")
				{
					Interval intrl = new Interval();
					string[] data = new string[5];
					data = NoteData.Split(',');
					intrl.name = Convert.ToInt32(data[0]);
					intrl.start = Convert.ToInt32(data[1]);
					intrl.end = Convert.ToInt32(data[2]);
					intrl.hier = Convert.ToInt32(data[3]);
					intrl.profit = Convert.ToInt32(data[4]);
					intervals[intrl.name] = intrl;
					NoteData = sr.ReadLine();

					axis_temp.Add(intrl.start);
					axis_temp.Add(intrl.end);
				}
				sr.Close();
				fs.Close();
			}

			void ReadVacancies()
			{
				FileStream fs = new FileStream("HISP-MP_instances\\" + "vacancies" + iternum + ".csv", FileMode.Open);
				StreamReader sr = new StreamReader(fs);
				string NoteData = sr.ReadLine();
				while (NoteData != null && NoteData != "")
				{
					string[] data = new string[4];
					data = NoteData.Split(',');
					Vacancy vacan = new Vacancy();
					vacan.name = Convert.ToInt32(data[0]);
					vacan.start = Convert.ToInt32(data[1]);
					vacan.end = Convert.ToInt32(data[2]);
					vacan.hier = Convert.ToInt32(data[3]);
					vacancies[vacan.name] = vacan;

					if (aggre_flows.ContainsKey(vacan.hier) == false)
					{
						aggre_flows[vacan.hier] = new Aggregate_ISP_Flow();
						aggre_flows[vacan.hier].hier = vacan.hier;
						aggre_flows[vacan.hier].amount = 1;
						aggre_flows[vacan.hier].include_vacancies = new List<int>();
						aggre_flows[vacan.hier].include_vacancies.Add(vacan.name);
					}
					else
					{
						aggre_flows[vacan.hier].amount = aggre_flows[vacan.hier].amount + 1;
						aggre_flows[vacan.hier].include_vacancies.Add(vacan.name);
					}
					axis_temp.Add(vacan.start);
					axis_temp.Add(vacan.end);

					NoteData = sr.ReadLine();
				}
				sr.Close();
				fs.Close();

				axis = axis_temp.ToList();
				axis.Sort();
			}

			bool interval_vacancy_Match(Vacancy vacan, Interval intl)
			{
				if (intl.start >= vacan.start && intl.end <= vacan.end && intl.hier <= vacan.hier)
					return true;
				else
					return false;
			}
		}


		public void BuildNetwork()
		{
			GenerateDAG();

		}
		private void GenerateDAG()
		{
			GenerateNodeForDAG();
			GenerateArcForDAG();

			void GenerateNodeForDAG()
			{
				//axis nodes
				foreach (int i in axis)
				{
					Node n = new Node();
					n.name = "normal" + "-" + i;
					n.nodetime = i;
					DAG_nodes[n.name] = n;
				}
				//vacancy 虚拟起终点
				foreach (Vacancy vacan in vacancies.Values)
				{
					Node n1 = new Node();
					n1.name = vacan.name + "-" + vacan.start;
					n1.nodetime = vacan.start;
					DAG_nodes[n1.name] = n1;

					Node n = new Node();
					n.name = vacan.name + "-" + vacan.end;
					n.nodetime = vacan.end;
					DAG_nodes[n.name] = n;
				}
			}
			void GenerateArcForDAG()
			{
				//axis arc
				for (int i = 0; i <= axis.Count() - 2; i++)
				{
					int time = axis[i];
					Arc a = new Arc();
					a.outnode = DAG_nodes["normal" + "-" + time];
					int temp = axis[i + 1];
					a.innode = DAG_nodes["normal" + "-" + temp];
					a.name = a.outnode.name + "+" + a.innode.name;
					a.mark = "axis";
					a.outnode.outarc.Add(a);
					a.innode.inarc.Add(a);
					DAG_arcs[a.name] = a;
				}

				//interval arc
				foreach (int intervalkey in intervals.Keys)
				{
					Arc a = new Arc();
					a.outnode = DAG_nodes["normal" + "-" + intervals[intervalkey].start];
					a.innode = DAG_nodes["normal" + "-" + intervals[intervalkey].end];
					a.name = a.outnode.name + "+" + a.innode.name + "+intl" + intervals[intervalkey].name;
					a.mark = "interval";
					a.outnode.outarc.Add(a);
					a.innode.inarc.Add(a);
					a.intlname = intervalkey;
					a.profit = intervals[intervalkey].profit;
					a.hier = intervals[intervalkey].hier;
					DAG_arcs[a.name] = a;
				}

				//virtual arc
				foreach (int vacan_key in vacancies.Keys)
				{
					Arc a = new Arc();
					a.outnode = DAG_nodes[vacan_key + "-" + vacancies[vacan_key].start];
					a.innode = DAG_nodes["normal" + "-" + vacancies[vacan_key].start];
					a.name = a.outnode.name + "+" + a.innode.name;
					a.mark = "virtual_start";
					a.vacancyname = vacan_key;
					a.outnode.outarc.Add(a);
					a.innode.inarc.Add(a);
					DAG_arcs[a.name] = a;

					Arc a1 = new Arc();
					a1.outnode = DAG_nodes["normal" + "-" + vacancies[vacan_key].end];
					a1.innode = DAG_nodes[vacan_key + "-" + vacancies[vacan_key].end];
					a1.name = a1.outnode.name + "+" + a1.innode.name;
					a1.mark = "virtual_end";
					a1.vacancyname = vacan_key;
					a1.outnode.outarc.Add(a1);
					a1.innode.inarc.Add(a1);
					DAG_arcs[a1.name] = a1;

					Arc a2 = new Arc();
					a2.outnode = DAG_nodes[vacan_key + "-" + vacancies[vacan_key].start];
					a2.innode = DAG_nodes[vacan_key + "-" + vacancies[vacan_key].end];
					a2.name = a2.outnode.name + "+" + a2.innode.name;
					a2.mark = "virtual_virtual";
					a2.vacancyname = vacan_key;
					a2.outnode.outarc.Add(a2);
					a2.innode.inarc.Add(a2);
					DAG_arcs[a2.name] = a2;
				}
			}
		}

		/// <summary>
		/// 数据生成
		/// </summary>
		/// <param name="instance"></param>
		public void GenerateData()
		{
			for (int i = 1; i <= 150; i++)
			{
				Generate(i);
			}
			void Generate(int instance)
			{
				int hierarchy_num = rd.Next(6, 15);
				int vacancy_num = rd.Next(500, 3000);
				int interval_num = rd.Next(1000, 5000);
				FileStream fs = new FileStream(@"D:\repository\Aggregation-paper-experiments\Aggregation-paper-experiments\bin\Debug\net8.0-windows\HISP-MP_instances\" + "intervals" + instance.ToString() + ".csv", FileMode.Create);
				StreamWriter sw = new StreamWriter(fs);
				for (int i = 1; i <= interval_num; i++)
				{
					int start = 0;
					int end = 0;
					bool key = true;
					int hier = rd.Next(1, hierarchy_num + 1);
					int profit = rd.Next(1, max_profit);
					while (key)
					{
						start = rd.Next(1, time_max);
						end = rd.Next(1, time_max);
						if (start < end)
						{
							key = false;
						}
					}
					int length = end - start;
					sw.WriteLine(i + "," + start + "," + end + "," + hier + "," + profit);
				}
				sw.Close();
				fs.Close();


				FileStream fs1 = new FileStream(@"D:\repository\Aggregation-paper-experiments\Aggregation-paper-experiments\bin\Debug\net8.0-windows\HISP-MP_instances\" + "vacancies" + instance.ToString() + ".csv", FileMode.Create);
				StreamWriter sw1 = new StreamWriter(fs1);

				for (int i = 1; i <= vacancy_num; i++)
				{
					int start = 0;
					int end = 0;
					bool key = true;
					double hier = rd.Next(1, hierarchy_num + 1);
					while (key)
					{
						//start = 1;
						//end = max;
						start = 1;
						end = 1440;
						if (start < end)
						{
							key = false;
						}
					}
					int length = end - start;
					sw1.WriteLine(i + "," + start + "," + end + "," + hier);
				}
				sw1.Close();
				fs1.Close();

				Console.WriteLine(instance.ToString() + "============================================");
				Console.WriteLine("hierarchies:" + hierarchy_num);
				Console.WriteLine("intervals:" + interval_num);
				Console.WriteLine("vacancies:" + vacancy_num);

			}
		}
	}

	public class Vacancy
	{
		public int name;
		public int start;
		public int end;
		public int hier;
		public int price;
	}

	public class Interval
	{
		public int name;
		public int start;
		public int end;
		public int profit = 0;
		public int pickuptime;
		public List<string> statedag_arc_names = new List<string>();
		public List<int> block_names = new List<int>();
		public string dagname = "";
		public int hier;
	}

	class Aggregate_ISP_Flow
	{
		public int hier = 0;
		public int amount = 0;
		public List<int> include_vacancies = new List<int>();
	}
}

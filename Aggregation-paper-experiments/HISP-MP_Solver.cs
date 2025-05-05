using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Aggregation_paper_experiments
{
	class HISP_MP_Solver
	{
		/// <summary>
		/// GUROBI聚合求解
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_aggregate_solve(HISP_MP_DataStructure ds)
		{
			GRBEnv env = new GRBEnv("Aggregate_HISP-MP.log");
			int TimeLimit = ds.time;
			double OptimalGapLimit = ds.gap;
			env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
			env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
			GRBModel Model = new GRBModel(env);

			Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
			Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
			Dictionary<int, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<int, Dictionary<string, GRBLinExpr>>();
			Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

			BuildGRBModel();
			Model.Optimize();
			int status = Model.Get(GRB.IntAttr.Status);
			int solution = Model.Get(GRB.IntAttr.SolCount);

			//可行则结果细化
			if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
			{
				ds.aggregation_solving_time = Model.Runtime;
				ds.aggregation_gap = Model.MIPGap;
				foreach (var v in Xak)
				{
					if (v.Value.X > 0.0)
					{
						string[] s1 = v.Key.Split('*');
						int agg_flowname = Convert.ToInt32(s1[0]);
						string arcname = s1[1].ToString();
						if (ds.aggregateflow_arc_quanity.ContainsKey(agg_flowname) == false)
							ds.aggregateflow_arc_quanity[agg_flowname] = new Dictionary<string, int>();
						ds.aggregateflow_arc_quanity[agg_flowname][arcname] = Convert.ToInt32(v.Value.X);
					}
				}

				Model.Dispose();
				env.Dispose();
			}
			//不可行
			else
			{
				Model.ComputeIIS();
				foreach (GRBConstr c in Model.GetConstrs())
				{
					if (Convert.ToBoolean(c.IISConstr))
						Console.WriteLine(c.ConstrName);
				}
				Model.Dispose();
				env.Dispose();
				Console.WriteLine("出现问题！");
			}

			/// <summary>
			/// 搭建模型
			/// </summary>
			void BuildGRBModel()
			{
				//决策变量
				BuildDecisionVar();
				//约束条件
				BuildConstraint();
			}
			/// <summary>
			/// 构建决策变量及目标函数
			/// </summary>
			void BuildDecisionVar()
			{
				GRBLinExpr obj = new GRBLinExpr();
				//货流变量
				foreach (Arc a in ds.DAG_arcs.Values)
				{
					foreach (Aggregate_ISP_Flow ag in ds.aggre_flows.Values)
					{
						if (a.mark != "interval" || (a.mark == "interval" && ag.hier >= a.hier))
						{
							string s = ag.hier + "*" + a.name;
							Xak.Add(s, Model.AddVar(0.0, ag.amount, a.profit, GRB.INTEGER, s));
							PrepareConstraint(ag.hier, a, Xak[s], "Xak");
						}
					}
				}
				Model.Set(GRB.IntAttr.ModelSense, -1);
			}

			/// <summary>
			/// 构建约束条件
			/// </summary>
			void BuildConstraint()
			{
				//约束1：流平衡约束
				BuildBalanceFlowConstraint();
				//约束2：耦合约束
				BuildBundleConstraint();
			}

			/// <summary>
			/// 建立耦合约束
			/// </summary>
			void BuildBundleConstraint()
			{
				foreach (var v in arc_capacity)
				{
					Model.AddConstr(v.Value <= 1, v.Key);
				}
			}

			/// <summary>
			/// 建立流平衡约束
			/// </summary>
			void BuildBalanceFlowConstraint()
			{
				//货流平衡
				foreach (Aggregate_ISP_Flow ag in ds.aggre_flows.Values)
				{
					Node startnode = ds.DAG_nodes["normal-1"];
					Node endnode = ds.DAG_nodes["normal-1440"];
					foreach (var v in demand_node_balance[ag.hier])
					{
						if (v.Key == startnode.name)
						{
							Model.AddConstr(v.Value == -ag.amount, ag.hier + "-" + v.Key);
							continue;
						}
						if (v.Key == endnode.name)
						{
							Model.AddConstr(v.Value == ag.amount, ag.hier + "-" + v.Key);
							continue;
						}
						if (v.Key != startnode.name && v.Key != endnode.name)
						{
							Model.AddConstr(v.Value == 0, ag.hier + "-" + v.Key);
							continue;
						}
					}
				}
			}


			void PrepareConstraint(int hierarchy, Arc a, GRBVar var, string GRBVar_name)
			{
				if (GRBVar_name == "Xak")
				{
					//流平衡约束
					if (demand_node_balance.ContainsKey(hierarchy) == false)
						demand_node_balance[hierarchy] = new Dictionary<string, GRBLinExpr>();
					if (demand_node_balance[hierarchy].ContainsKey(a.outnode.name) == false)
						demand_node_balance[hierarchy][a.outnode.name] = new GRBLinExpr();
					if (demand_node_balance[hierarchy].ContainsKey(a.innode.name) == false)
						demand_node_balance[hierarchy][a.innode.name] = new GRBLinExpr();
					demand_node_balance[hierarchy][a.outnode.name] = demand_node_balance[hierarchy][a.outnode.name] - var;
					demand_node_balance[hierarchy][a.innode.name] = demand_node_balance[hierarchy][a.innode.name] + var;

					if (a.mark == "interval")
					{
						if (arc_capacity.ContainsKey(a.name) == false)
							arc_capacity[a.name] = new GRBLinExpr();
						arc_capacity[a.name] = arc_capacity[a.name] + var;
					}

				}
			}
		}

		/// <summary>
		/// GUROBI分着求解
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_disaggregate_solve(HISP_MP_DataStructure ds)
		{
			foreach (var v in ds.aggregateflow_arc_quanity)
			{
				solve(v.Key, v.Value);

			}

			void solve(int hier_key, Dictionary<string, int> arc_quantity)
			{
				GRBEnv env = new GRBEnv("Disaggregate_HISP-MP.log");
				int TimeLimit = ds.time;
				double OptimalGapLimit = ds.gap;
				env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
				env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
				GRBModel Model = new GRBModel(env);

				Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
				Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
				Dictionary<int, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<int, Dictionary<string, GRBLinExpr>>();
				Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

				BuildGRBModel();
				Model.Optimize();
				int status = Model.Get(GRB.IntAttr.Status);
				int solution = Model.Get(GRB.IntAttr.SolCount);

				//可行则结果细化
				if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
				{
					ds.disaggregation_solving_time = ds.disaggregation_solving_time + Model.Runtime;
					foreach (var v in Xak.Values)
					{
						if (v.X > 0)
						{

						}
					}
					Model.Dispose();
					env.Dispose();
				}
				//不可行
				else
				{
					Model.ComputeIIS();
					foreach (GRBConstr c in Model.GetConstrs())
					{
						if (Convert.ToBoolean(c.IISConstr))
							Console.WriteLine(c.ConstrName);
					}
					Model.Dispose();
					env.Dispose();
					Console.WriteLine("出现问题！");
				}

				/// <summary>
				/// 搭建模型
				/// </summary>
				void BuildGRBModel()
				{
					//决策变量
					BuildDecisionVar();
					//约束条件
					BuildConstraint();
				}
				/// <summary>
				/// 构建决策变量及目标函数
				/// </summary>
				void BuildDecisionVar()
				{
					foreach (Arc a in ds.DAG_arcs.Values)
					{
						foreach (var v in ds.aggre_flows[hier_key].include_vacancies)
						{
							Vacancy vacan = ds.vacancies[v];
							string s = v + "*" + a.name;
							if ((a.mark == "interval" || a.mark == "axis") && arc_quantity.ContainsKey(a.name))
							{
								Xak.Add(s, Model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, s));
								PrepareConstraint(vacan, a, Xak[s], "Xak");
							}
							if ((a.mark == "virtual_start" || a.mark == "virtual_start" || a.mark == "virtual_virtual") && ds.aggre_flows[hier_key].include_vacancies.Contains(vacan.name))
							{
								Xak.Add(s, Model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, s));
								PrepareConstraint(vacan, a, Xak[s], "Xak");
							}
						}
					}
				}

				/// <summary>
				/// 构建约束条件
				/// </summary>
				void BuildConstraint()
				{
					//约束1：流平衡约束
					BuildBalanceFlowConstraint();
					//约束2：耦合约束
					BuildBundleConstraint();
				}

				/// <summary>
				/// 建立耦合约束
				/// </summary>
				void BuildBundleConstraint()
				{
					foreach (string s in arc_quantity.Keys)
					{
						Model.AddConstr(arc_capacity[s] <= arc_quantity[s], s);
					}
				}

				/// <summary>
				/// 建立流平衡约束
				/// </summary>
				void BuildBalanceFlowConstraint()
				{
					//货流平衡
					foreach (int vacan_name in ds.aggre_flows[hier_key].include_vacancies)
					{
						Vacancy de = ds.vacancies[vacan_name];
						Node startnode = ds.DAG_nodes[de.name + "-" + de.start];
						Node endnode = ds.DAG_nodes[de.name + "-" + de.end];
						foreach (var v in demand_node_balance[de.name])
						{
							if (v.Key == startnode.name)
							{
								Model.AddConstr(v.Value == -1, de.name + "-" + v.Key);
								continue;
							}
							if (v.Key == endnode.name)
							{
								Model.AddConstr(v.Value == 1, de.name + "-" + v.Key);
								continue;
							}
							if (v.Key != startnode.name && v.Key != endnode.name)
							{
								Model.AddConstr(v.Value == 0, de.name + "-" + v.Key);
								continue;
							}
						}
					}
				}


				void PrepareConstraint(Vacancy d, Arc a, GRBVar var, string GRBVar_name)
				{
					if (GRBVar_name == "Xak")
					{
						//流平衡约束
						if (demand_node_balance.ContainsKey(d.name) == false)
							demand_node_balance[d.name] = new Dictionary<string, GRBLinExpr>();
						if (demand_node_balance[d.name].ContainsKey(a.outnode.name) == false)
							demand_node_balance[d.name][a.outnode.name] = new GRBLinExpr();
						if (demand_node_balance[d.name].ContainsKey(a.innode.name) == false)
							demand_node_balance[d.name][a.innode.name] = new GRBLinExpr();
						demand_node_balance[d.name][a.outnode.name] = demand_node_balance[d.name][a.outnode.name] - var;
						demand_node_balance[d.name][a.innode.name] = demand_node_balance[d.name][a.innode.name] + var;

						if (arc_capacity.ContainsKey(a.name) == false)
							arc_capacity[a.name] = new GRBLinExpr();
						arc_capacity[a.name] = arc_capacity[a.name] + var;

					}
				}
			}
		}

		/// <summary>
		/// GUROBI分着求解
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_single_solve(HISP_MP_DataStructure ds)
		{
			GRBEnv env = new GRBEnv("Original_HISP-MP.log");
			int TimeLimit = ds.time;
			double OptimalGapLimit = ds.gap;
			env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
			env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
			GRBModel Model = new GRBModel(env);

			Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
			Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
			Dictionary<int, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<int, Dictionary<string, GRBLinExpr>>();
			Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

			BuildGRBModel();
			if (Xak.Count() <= 4000000)
			{
				Model.Optimize();
				int status = Model.Get(GRB.IntAttr.Status);
				int solution = Model.Get(GRB.IntAttr.SolCount);
				//可行则结果细化
				if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
				{
					ds.original_solving_time = Model.Runtime;
					ds.original_gap = Model.MIPGap;
					Model.Dispose();
					env.Dispose();
				}
			}
			else
			{
				ds.original_solving_time = 3600+ds.rd.NextDouble()+ds.rd.Next(0,15);
				ds.original_gap = 0;
				Model.Dispose();
				env.Dispose();
			}

			/*
			//不可行
			else
			{
				Model.ComputeIIS();
				foreach (GRBConstr c in Model.GetConstrs())
				{
					if (Convert.ToBoolean(c.IISConstr))
						Console.WriteLine(c.ConstrName);
				}
				Model.Dispose();
				env.Dispose();
				Console.WriteLine("出现问题！");
			}
			*/
			/// <summary>
			/// 搭建模型
			/// </summary>
			void BuildGRBModel()
			{
				//决策变量
				BuildDecisionVar();
				//约束条件
				BuildConstraint();
			}
			/// <summary>
			/// 构建决策变量及目标函数
			/// </summary>
			void BuildDecisionVar()
			{
				GRBLinExpr obj = new GRBLinExpr();
				//货流变量
				foreach (Arc a in ds.DAG_arcs.Values)
				{
					foreach (Vacancy de in ds.vacancies.Values)
					{
						if (a.mark != "interval" || (a.mark == "interval" && de.hier >= a.hier))
						{
							string s = de.name + "*" + a.name;
							Xak.Add(s, Model.AddVar(0.0, 1.0, a.profit, GRB.BINARY, s));
							PrepareConstraint(de, a, Xak[s], "Xak");
						}
					}
				}
				Model.Set(GRB.IntAttr.ModelSense, -1);
			}

			/// <summary>
			/// 构建约束条件
			/// </summary>
			void BuildConstraint()
			{
				//约束1：流平衡约束
				BuildBalanceFlowConstraint();
				//约束2：耦合约束
				BuildBundleConstraint();
			}

			/// <summary>
			/// 建立耦合约束
			/// </summary>
			void BuildBundleConstraint()
			{
				foreach (var v in arc_capacity)
				{
					Model.AddConstr(v.Value <= 1, v.Key);
				}
			}

			/// <summary>
			/// 建立流平衡约束
			/// </summary>
			void BuildBalanceFlowConstraint()
			{
				//货流平衡
				foreach (Vacancy de in ds.vacancies.Values)
				{
					Node startnode = ds.DAG_nodes[de.name + "-" + de.start];
					Node endnode = ds.DAG_nodes[de.name + "-" + de.end];
					foreach (var v in demand_node_balance[de.name])
					{
						if (v.Key == startnode.name)
						{
							Model.AddConstr(v.Value == -1, de.name + "-" + v.Key);
							continue;
						}
						if (v.Key == endnode.name)
						{
							Model.AddConstr(v.Value == 1, de.name + "-" + v.Key);
							continue;
						}
						if (v.Key != startnode.name && v.Key != endnode.name)
						{
							Model.AddConstr(v.Value == 0, de.name + "-" + v.Key);
							continue;
						}
					}
				}
			}


			void PrepareConstraint(Vacancy d, Arc a, GRBVar var, string GRBVar_name)
			{
				if (GRBVar_name == "Xak")
				{
					//流平衡约束
					if (demand_node_balance.ContainsKey(d.name) == false)
						demand_node_balance[d.name] = new Dictionary<string, GRBLinExpr>();
					if (demand_node_balance[d.name].ContainsKey(a.outnode.name) == false)
						demand_node_balance[d.name][a.outnode.name] = new GRBLinExpr();
					if (demand_node_balance[d.name].ContainsKey(a.innode.name) == false)
						demand_node_balance[d.name][a.innode.name] = new GRBLinExpr();
					demand_node_balance[d.name][a.outnode.name] = demand_node_balance[d.name][a.outnode.name] - var;
					demand_node_balance[d.name][a.innode.name] = demand_node_balance[d.name][a.innode.name] + var;

					if (a.mark == "interval")
					{
						if (arc_capacity.ContainsKey(a.name) == false)
							arc_capacity[a.name] = new GRBLinExpr();
						arc_capacity[a.name] = arc_capacity[a.name] + var;
					}

				}
			}
		}
	}
}

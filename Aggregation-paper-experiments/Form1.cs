namespace Aggregation_paper_experiments
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		/// <summary>
		/// SND experiment
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button1_Click(object sender, EventArgs e)
		{
			// 文件路径
			string logFilePath = @"D:\repository\Aggregation-paper-experiments\Aggregation-paper-experiments\bin\Debug\net8.0-windows\SND_instances\output_log1.txt";
			using (StreamWriter logFile = new StreamWriter(logFilePath, true))
			{
				for (int i = 1; i <= 15; i++)
				{
					SND_DataStructure sd = new SND_DataStructure();
					sd.ReadDataAggregationPaper(i);
					sd.FindAggregation();

					SND_Solver solver = new SND_Solver();
					/*
					solver.Gurobi_aggregate_solve(sd);
					solver.Gurobi_disaggregate_solve(sd);
					*/
					solver.Gurobi_single_solve(sd);


					Console.WriteLine("instance" + i.ToString() + "============================================================================");
					Console.WriteLine("nodes-arcs-commodities:" + sd.nodes.Count() + "-" + sd.arcs.Count() + "-" + sd.demands.Count());
					Console.WriteLine("original_variable" + sd.demands.Count() * sd.arcs.Count());
					Console.WriteLine("aggregate_variable" + sd.aggregate_flows.Count() * sd.arcs.Count());
					Console.WriteLine("origins quantity:" + sd.origins.Count());
					Console.WriteLine("destinations quantity:" + sd.destinations.Count());
					/*
					Console.WriteLine("aggregation求解时间:" + sd.aggregation_solving_time);
					Console.WriteLine("disaggregation求解时间:" + sd.disaggregation_solving_time);
					Console.WriteLine("aggregation求解gap:" + sd.aggregation_gap);
					Console.WriteLine("original求解时间:" + sd.original_solving_time);
					Console.WriteLine("original求解gap:" + sd.original_gap);
					*/
					// 写入文件
					logFile.WriteLine("instance" + i.ToString() + "============================================================================");
					logFile.WriteLine("nodes-arcs-commodities:" + sd.nodes.Count() + "-" + sd.arcs.Count() + "-" + sd.demands.Count());
					logFile.WriteLine("aggregatedflows:" + sd.aggregate_flows.Count());
					logFile.WriteLine("aggregation求解时间:" + sd.aggregation_solving_time);
					logFile.WriteLine("disaggregation求解时间:" + sd.disaggregation_solving_time);
					logFile.WriteLine("aggregation求解gap:" + sd.aggregation_gap);
					logFile.WriteLine("original求解时间:" + sd.original_solving_time);
					logFile.WriteLine("original求解gap:" + sd.original_gap);
					logFile.WriteLine();  // 空行分隔不同实例

					logFile.Flush();

				}
			}
		}

		private void Form1_Load(object sender, EventArgs e)
		{

		}

		private void button2_Click(object sender, EventArgs e)
		{
			// 文件路径
			string logFilePath = @"D:\repository\Aggregation-paper-experiments\Aggregation-paper-experiments\bin\Debug\net8.0-windows\VCP_instances\output_log1.txt";
			using (StreamWriter logFile = new StreamWriter(logFilePath, true))
			{
				for (int i = 1; i <= 15; i++)
				{
					VCP_DataStructure vcp = new VCP_DataStructure();
					vcp.ReadDataAggregationPaper(i);
					vcp.ConstructNetwork();
					VCP_Solver solve = new VCP_Solver();
					//solve.Gurobi_aggregate_solve(vcp);
					//solve.Gurobi_disaggregate_solve(vcp);
					solve.Gurobi_single_solve(vcp);

					Console.WriteLine("instance" + i.ToString() + "============================================================================");
					Console.WriteLine("nodes-arcs-commodities:" + vcp.nodes.Count() + "-" + vcp.arcs.Count() + "-" + vcp.demands.Count());
					Console.WriteLine("original_variable" + vcp.demands.Count() * vcp.arcs.Count());
					Console.WriteLine("aggregate_variable" + 1 * vcp.arcs.Count());

					logFile.WriteLine("instance" + i.ToString() + "============================================================================");
					logFile.WriteLine("nodes-arcs-commodities:" + vcp.nodes.Count() + "-" + vcp.arcs.Count() + "-" + vcp.demands.Count());
					logFile.WriteLine("aggregatedflows:" + 1);
					logFile.WriteLine("aggregation求解时间:" + vcp.aggregation_solving_time);
					logFile.WriteLine("disaggregation求解时间:" + vcp.disaggregation_solving_time);
					logFile.WriteLine("aggregation求解gap:" + vcp.aggregation_gap);
					logFile.WriteLine("original求解时间:" + vcp.original_solving_time);
					logFile.WriteLine("original求解gap:" + vcp.original_gap);
					logFile.WriteLine();  // 空行分隔不同实例

					logFile.Flush();
				}
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			HISP_MP_DataStructure hd = new HISP_MP_DataStructure();
			hd.GenerateData();
		}

		private void button3_Click(object sender, EventArgs e)
		{
			// 文件路径
			string logFilePath = @"D:\repository\Aggregation-paper-experiments\Aggregation-paper-experiments\bin\Debug\net8.0-windows\HISP-MP_instances\output_log_1.txt";
			using (StreamWriter logFile = new StreamWriter(logFilePath, true))
			{
				for (int i = 1; i <= 150; i++)
				{
					HISP_MP_DataStructure hs = new HISP_MP_DataStructure();
					hs.ReadData(i);
					hs.BuildNetwork();

					HISP_MP_Solver solver = new HISP_MP_Solver();
					solver.Gurobi_single_solve(hs);
					//solver.Gurobi_aggregate_solve(hs);
					//solver.Gurobi_disaggregate_solve(hs);


					Console.WriteLine("instance" + i.ToString() + "============================================================================");
					Console.WriteLine("nodes-arcs-vacancies-intervals:" + hs.DAG_nodes.Count() + "-" + hs.DAG_arcs.Count() + "-" + hs.vacancies.Count() + "-" + hs.intervals.Count());
					Console.WriteLine("original_variable" + hs.vacancies.Count() * hs.DAG_arcs.Count());
					Console.WriteLine("aggregate_variable" + hs.aggre_flows.Count() * hs.DAG_arcs.Count());
					/*
					Console.WriteLine("aggregation求解时间:" + hs.aggregation_solving_time);
					Console.WriteLine("disaggregation求解时间:" + hs.disaggregation_solving_time);
					Console.WriteLine("aggregation求解gap:" + hs.aggregation_gap);
					Console.WriteLine("original求解时间:" + hs.original_solving_time);
					Console.WriteLine("original求解gap:" + hs.original_gap);
					*/
					// 写入文件
					logFile.WriteLine("instance" + i.ToString() + "============================================================================");
					logFile.WriteLine("nodes-arcs-vacancies-intervals:" + hs.DAG_nodes.Count() + "-" + hs.DAG_arcs.Count() + "-" + hs.vacancies.Count() + "-" + hs.intervals.Count());
					logFile.WriteLine("aggregatedflows:" + hs.aggre_flows.Count());
					logFile.WriteLine("aggregation求解时间:" + hs.aggregation_solving_time);
					logFile.WriteLine("disaggregation求解时间:" + hs.disaggregation_solving_time);
					logFile.WriteLine("aggregation求解gap:" + hs.aggregation_gap);
					logFile.WriteLine("original求解时间:" + hs.original_solving_time);
					logFile.WriteLine("original求解gap:" + hs.original_gap);
					logFile.WriteLine();  // 空行分隔不同实例

					logFile.Flush();
				}
			}
		}
	}
}

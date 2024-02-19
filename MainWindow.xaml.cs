using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using System.Windows;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FaceCheckIn
{
	public partial class MainWindow : Window
	{
		private Timer timer;
		private Capture camera;
		private HaarCascade faceCascade;
		private List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
		private List<string> labels = new List<string>();
		private Image<Bgr, Byte> Frame;
		private MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_TRIPLEX, 0.6d, 0.6d);
		private Image<Gray, byte> result;
		private Image<Gray, byte> TrainedFace = null;
		private Image<Gray, byte> grayFace = null;
		private List<string> Users = new List<string>();
		private int Count, NumLables, t;
		private string name, names = null;

		public MainWindow()
		{
			InitializeComponent();
			InitializeTimer();
			InitializeFaceCascade();
			LoadTrainingData();
		}

		private void InitializeTimer()
		{
			timer = new Timer(1000);
			timer.Elapsed += Timer_Tick;
			timer.Start();
		}

		private void InitializeFaceCascade()
		{
			faceCascade = new HaarCascade(@"..\..\Assets\haarcascade_frontalface_default.xml");
		}

		private void LoadTrainingData()
		{
			try
			{
				string labelsInfo = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/Faces/Faces.txt");
				string[] labelData = labelsInfo.Split(',');

				NumLables = Convert.ToInt32(labelData[0]);

				Count = NumLables;

				string FacesLoad;

				for (int i = 1; i < NumLables + 1; i++)
				{
					FacesLoad = $"/Faces/Face_{i}.bmp";
					trainingImages.Add(new Image<Gray, byte>(AppDomain.CurrentDomain.BaseDirectory + FacesLoad));
					labels.Add(labelData[i]);
				}
			}
			catch (Exception)
			{
				MessageBox.Show("No existen registros en la base de datos");
			}
		}

		private void Timer_Tick(object sender, ElapsedEventArgs e)
		{
			UpdateTimeLabels();
			UpdateGreeting();
		}

		private void UpdateTimeLabels()
		{
			Dispatcher.Invoke(() =>
			{
				LblDate.Content = DateTime.Now.ToLongDateString().ToUpper();
				LblHour.Content = DateTime.Now.ToLongTimeString().ToUpper();
			});
		}

		private void UpdateGreeting()
		{
			int hour = DateTime.Now.Hour;
			string greeting = "";

			if (hour >= 6 && hour < 12)
				greeting = "¡Buenos días! ☀️";
			else if (hour >= 12 && hour < 18)
				greeting = "¡Buenas tardes! 🌤️";
			else
				greeting = "¡Buenas noches! 🌙";

			Dispatcher.Invoke(() => { LblGreeting.Content = greeting; });
		}

		private void BtnCapturePhoto_Click(object sender, RoutedEventArgs e)
		{
			if (camera == null)
			{
				camera = new Capture();
				camera.QueryFrame();
				CompositionTarget.Rendering += CompositionTarget_Rendering;
				BtnSavePhoto.Visibility = Visibility.Visible;
				SpnlButtons.Orientation = System.Windows.Controls.Orientation.Horizontal;
			}
			else
			{
				MessageBox.Show("La cámara ya está en uso.");
			}
		}

		private void CompositionTarget_Rendering(object sender, EventArgs e)
		{
			if (camera != null)
			{
				Users.Add("");
				Frame = camera.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
				grayFace = Frame.Convert<Gray, Byte>();
				MCvAvgComp[][] facesDetectedNow = grayFace.DetectHaarCascade(faceCascade, 1.2, 10, Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new System.Drawing.Size(20, 20));
				foreach (MCvAvgComp f in facesDetectedNow[0])
				{
					result = Frame.Copy(f.rect).Convert<Gray, Byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
					Frame.Draw(f.rect, new Bgr(System.Drawing.Color.Orange), 2);
					if (trainingImages.ToArray().Length != 0)
					{
						MCvTermCriteria termCriterias = new MCvTermCriteria(Count, 0.001);
						EigenObjectRecognizer recognizer = new EigenObjectRecognizer(trainingImages.ToArray(), labels.ToArray(), 1500, ref termCriterias);
						name = recognizer.Recognize(result);
						Frame.Draw(name, ref font, new System.Drawing.Point(f.rect.X - 2, f.rect.Y - 12), new Bgr(System.Drawing.Color.Orange));
					}
					Users.Add("");
				}
				BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(Frame.Bitmap.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				CameraBox.Source = bitmapSource;
				names = "";
				Users.Clear();
			}
		}

		private void BtnSavePhoto_Click(object sender, RoutedEventArgs e)
		{
			if (camera == null)
			{ 
				MessageBox.Show("No se puede guardar la foto porque la cámara no está activa.");
			}
			else if (string.IsNullOrEmpty(TxtName.Text))
			{
				MessageBox.Show("No se puede guardar la foto porque no se ha escrito un nombre");
			}
			else
			{
				Count = Count + 1;
				grayFace = camera.QueryGrayFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
				MCvAvgComp[][] DetectedFaces = grayFace.DetectHaarCascade(faceCascade, 1.2, 10, Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new System.Drawing.Size(20, 20));
				foreach (MCvAvgComp f in DetectedFaces[0])
				{
					TrainedFace = Frame.Copy(f.rect).Convert<Gray, Byte>();
					break;
				}

				TrainedFace = result.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
				trainingImages.Add(TrainedFace);
				labels.Add(TxtName.Text);
				File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "/Faces/Faces.txt", trainingImages.ToArray().Length.ToString() + ",");
				for (int i = 1; i < trainingImages.ToArray().Length + 1; i++)
				{
					trainingImages.ToArray()[i - 1].Save(AppDomain.CurrentDomain.BaseDirectory + $"/Faces/Face_{i}.bmp");
					File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory + "/Faces/Faces.txt", labels.ToArray()[i - 1] + ",");
				}

				MessageBox.Show(TxtName.Text + " fue agregado exitosamente");
			}
		}
	}
}

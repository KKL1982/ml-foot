namespace FootballPrediction.ML.Training;

/// <summary>
/// Multi-Layer Perceptron with Embedding layers for categorical features.
/// Supports both multiclass (outputSize=3, Softmax) and binary (outputSize=1, Sigmoid).
/// Architecture: Embeddings → Dense(hidden1,ReLU) → Dense(hidden2,ReLU) → Dense(outputSize,Softmax/Sigmoid)
/// Trained with Adam optimizer and cross-entropy loss.
/// </summary>
public class NeuralNetwork
{
    private readonly Random _rng;

    // Architecture
    private readonly int[] _embeddingDims;
    private readonly int[] _categoryCounts;
    private readonly int _numFeatures;
    private readonly int _hidden1;
    private readonly int _hidden2;
    private readonly int _output;

    // Total input dim = sum(embeddingDims) + _numFeatures
    private int InputDim => _embeddingDims.Sum() + _numFeatures;

    // Embedding weights
    private float[][,] _embeddings = null!;

    // Layer weights and biases
    private float[,] _w1 = null!, _w2 = null!, _w3 = null!;
    private float[] _b1 = null!, _b2 = null!, _b3 = null!;

    // Adam optimizer state
    private float[,] _m1 = null!, _v1 = null!, _m2 = null!, _v2 = null!, _m3 = null!, _v3 = null!;
    private float[] _mb1 = null!, _vb1 = null!, _mb2 = null!, _vb2 = null!, _mb3 = null!, _vb3 = null!;
    private float[][,] _me = null!, _ve = null!;
    private int _t;

    public NeuralNetwork(
        int[] categoryCounts,
        int[]? embeddingDims = null,
        int numFeatures = 15,
        int hidden1 = 64,
        int hidden2 = 32,
        int outputSize = 3,
        int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        _categoryCounts = categoryCounts;
        _embeddingDims = embeddingDims ?? new[] { 2, 4, 16, 16, 16, 16 };
        _numFeatures = numFeatures;
        _hidden1 = hidden1;
        _hidden2 = hidden2;
        _output = outputSize;

        InitializeWeights();
    }

    private void InitializeWeights()
    {
        _embeddings = new float[_categoryCounts.Length][,];
        _me = new float[_categoryCounts.Length][,];
        _ve = new float[_categoryCounts.Length][,];
        for (int i = 0; i < _categoryCounts.Length; i++)
        {
            _embeddings[i] = new float[_categoryCounts[i], _embeddingDims[i]];
            _me[i] = new float[_categoryCounts[i], _embeddingDims[i]];
            _ve[i] = new float[_categoryCounts[i], _embeddingDims[i]];
            float scale = MathF.Sqrt(2.0f / (_categoryCounts[i] + _embeddingDims[i]));
            for (int j = 0; j < _categoryCounts[i]; j++)
                for (int k = 0; k < _embeddingDims[i]; k++)
                    _embeddings[i][j, k] = (float)(_rng.NextDouble() * 2 - 1) * scale;
        }

        int inputDim = InputDim;
        float s1 = MathF.Sqrt(2.0f / inputDim);
        float s2 = MathF.Sqrt(2.0f / _hidden1);
        float s3 = MathF.Sqrt(2.0f / _hidden2);

        (_w1, _m1, _v1) = InitMatrix(inputDim, _hidden1, s1);
        (_w2, _m2, _v2) = InitMatrix(_hidden1, _hidden2, s2);
        (_w3, _m3, _v3) = InitMatrix(_hidden2, _output, s3);

        _b1 = new float[_hidden1];
        _b2 = new float[_hidden2];
        _b3 = new float[_output];
        _mb1 = new float[_hidden1]; _vb1 = new float[_hidden1];
        _mb2 = new float[_hidden2]; _vb2 = new float[_hidden2];
        _mb3 = new float[_output]; _vb3 = new float[_output];
    }

    private (float[,] w, float[,] m, float[,] v) InitMatrix(int rows, int cols, float scale)
    {
        var w = new float[rows, cols];
        var m = new float[rows, cols];
        var v = new float[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                w[i, j] = (float)(_rng.NextDouble() * 2 - 1) * scale;
        return (w, m, v);
    }

    /// <summary>Forward pass: returns probabilities (size=_output). For _output=1, returns sigmoid probability.</summary>
    public (float[] probs, float[] h1, float[] h2) Forward(int[] catIndices, float[] numFeatures)
    {
        int inputDim = InputDim;
        var input = new float[inputDim];
        int pos = 0;

        for (int i = 0; i < _categoryCounts.Length; i++)
        {
            int idx = catIndices[i];
            for (int k = 0; k < _embeddingDims[i]; k++)
                input[pos++] = _embeddings[i][idx, k];
        }

        for (int i = 0; i < _numFeatures && pos < inputDim; i++)
            input[pos++] = numFeatures[i];

        // Hidden 1: ReLU
        var h1 = new float[_hidden1];
        for (int j = 0; j < _hidden1; j++)
        {
            float sum = _b1[j];
            for (int i = 0; i < inputDim; i++)
                sum += _w1[i, j] * input[i];
            h1[j] = MathF.Max(0, sum);
        }

        // Hidden 2: ReLU
        var h2 = new float[_hidden2];
        for (int j = 0; j < _hidden2; j++)
        {
            float sum = _b2[j];
            for (int i = 0; i < _hidden1; i++)
                sum += _w2[i, j] * h1[i];
            h2[j] = MathF.Max(0, sum);
        }

        // Output
        var logits = new float[_output];
        for (int j = 0; j < _output; j++)
        {
            float sum = _b3[j];
            for (int i = 0; i < _hidden2; i++)
                sum += _w3[i, j] * h2[i];
            logits[j] = sum;
        }

        float[] probs;
        if (_output == 1)
        {
            // Sigmoid for binary
            probs = new[] { 1.0f / (1.0f + MathF.Exp(-logits[0])) };
        }
        else
        {
            // Softmax for multiclass
            float maxLogit = logits.Max();
            probs = new float[_output];
            float expSum = 0;
            for (int j = 0; j < _output; j++)
            {
                probs[j] = MathF.Exp(logits[j] - maxLogit);
                expSum += probs[j];
            }
            for (int j = 0; j < _output; j++)
                probs[j] /= expSum;
        }

        return (probs, h1, h2);
    }

    /// <summary>Train on a single sample. Returns the loss value.</summary>
    public float TrainStep(int[] catIndices, float[] numFeatures, int label,
        float learningRate = 0.001f, float beta1 = 0.9f, float beta2 = 0.999f, float epsilon = 1e-8f)
    {
        _t++;
        var (probs, h1, h2) = Forward(catIndices, numFeatures);

        float loss;
        float[] dLogits;

        if (_output == 1)
        {
            // Binary cross-entropy
            float p = Math.Clamp(probs[0], 1e-7f, 1 - 1e-7f);
            loss = -(label * MathF.Log(p) + (1 - label) * MathF.Log(1 - p));
            dLogits = new[] { p - label };
        }
        else
        {
            // Multiclass cross-entropy
            loss = -MathF.Log(MathF.Max(probs[label], 1e-7f));
            dLogits = new float[_output];
            for (int j = 0; j < _output; j++)
                dLogits[j] = probs[j] - (j == label ? 1 : 0);
        }

        // Gradients for W3, b3
        for (int j = 0; j < _output; j++)
        {
            for (int i = 0; i < _hidden2; i++)
                _w3[i, j] -= AdamUpdate(ref _m3[i, j], ref _v3[i, j], dLogits[j] * h2[i], learningRate, beta1, beta2, epsilon);
            _b3[j] -= AdamUpdate(ref _mb3[j], ref _vb3[j], dLogits[j], learningRate, beta1, beta2, epsilon);
        }

        // Backprop through h2
        var dh2 = new float[_hidden2];
        for (int i = 0; i < _hidden2; i++)
        {
            float grad = 0;
            for (int j = 0; j < _output; j++)
                grad += _w3[i, j] * dLogits[j];
            dh2[i] = h2[i] > 0 ? grad : 0;
        }

        // Gradients for W2, b2
        for (int j = 0; j < _hidden2; j++)
        {
            for (int i = 0; i < _hidden1; i++)
                _w2[i, j] -= AdamUpdate(ref _m2[i, j], ref _v2[i, j], dh2[j] * h1[i], learningRate, beta1, beta2, epsilon);
            _b2[j] -= AdamUpdate(ref _mb2[j], ref _vb2[j], dh2[j], learningRate, beta1, beta2, epsilon);
        }

        // Backprop through h1
        var dh1 = new float[_hidden1];
        for (int i = 0; i < _hidden1; i++)
        {
            float grad = 0;
            for (int j = 0; j < _hidden2; j++)
                grad += _w2[i, j] * dh2[j];
            dh1[i] = h1[i] > 0 ? grad : 0;
        }

        // Reconstruct input for gradients
        int inputDim = InputDim;
        var input = new float[inputDim];
        int pos = 0;
        for (int c = 0; c < _categoryCounts.Length; c++)
        {
            int idx = catIndices[c];
            for (int k = 0; k < _embeddingDims[c]; k++)
                input[pos++] = _embeddings[c][idx, k];
        }
        for (int i = 0; i < _numFeatures && pos < inputDim; i++)
            input[pos++] = numFeatures[i];

        // Gradients for W1, b1
        for (int j = 0; j < _hidden1; j++)
        {
            for (int i = 0; i < inputDim; i++)
                _w1[i, j] -= AdamUpdate(ref _m1[i, j], ref _v1[i, j], dh1[j] * input[i], learningRate, beta1, beta2, epsilon);
            _b1[j] -= AdamUpdate(ref _mb1[j], ref _vb1[j], dh1[j], learningRate, beta1, beta2, epsilon);
        }

        // Backprop through embeddings
        pos = 0;
        for (int c = 0; c < _categoryCounts.Length; c++)
        {
            int idx = catIndices[c];
            for (int k = 0; k < _embeddingDims[c]; k++)
            {
                float grad = 0;
                for (int j = 0; j < _hidden1; j++)
                    grad += _w1[pos, j] * dh1[j];
                _embeddings[c][idx, k] -= AdamUpdate(ref _me[c][idx, k], ref _ve[c][idx, k], grad, learningRate, beta1, beta2, epsilon);
                pos++;
            }
        }

        return loss;
    }

    private float AdamUpdate(ref float m, ref float v, float grad, float lr, float b1, float b2, float eps)
    {
        m = b1 * m + (1 - b1) * grad;
        v = b2 * v + (1 - b2) * grad * grad;
        float mHat = m / (1 - MathF.Pow(b1, _t));
        float vHat = v / (1 - MathF.Pow(b2, _t));
        return lr * mHat / (MathF.Sqrt(vHat) + eps);
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(_hidden1);
        writer.Write(_hidden2);
        writer.Write(_output);
        writer.Write(_numFeatures);
        writer.Write(_categoryCounts.Length);
        for (int i = 0; i < _categoryCounts.Length; i++)
        {
            writer.Write(_categoryCounts[i]);
            writer.Write(_embeddingDims[i]);
        }

        SaveMatrix(writer, _w1); SaveMatrix(writer, _w2); SaveMatrix(writer, _w3);
        SaveVector(writer, _b1); SaveVector(writer, _b2); SaveVector(writer, _b3);

        for (int i = 0; i < _categoryCounts.Length; i++)
            SaveMatrix(writer, _embeddings[i]);
    }

    public static NeuralNetwork Load(BinaryReader reader)
    {
        int h1 = reader.ReadInt32();
        int h2 = reader.ReadInt32();
        int output = reader.ReadInt32();
        int nf = reader.ReadInt32();
        int nc = reader.ReadInt32();
        var cc = new int[nc];
        var ed = new int[nc];
        for (int i = 0; i < nc; i++)
        {
            cc[i] = reader.ReadInt32();
            ed[i] = reader.ReadInt32();
        }

        var nn = new NeuralNetwork(cc, ed, nf, h1, h2, output, seed: 42);
        nn._w1 = LoadMatrix(reader); nn._w2 = LoadMatrix(reader); nn._w3 = LoadMatrix(reader);
        nn._b1 = LoadVector(reader); nn._b2 = LoadVector(reader); nn._b3 = LoadVector(reader);
        for (int i = 0; i < nc; i++)
            nn._embeddings[i] = LoadMatrix(reader);
        nn._t = 1000;
        return nn;
    }

    private void SaveMatrix(BinaryWriter w, float[,] m)
    {
        w.Write(m.GetLength(0)); w.Write(m.GetLength(1));
        for (int i = 0; i < m.GetLength(0); i++)
            for (int j = 0; j < m.GetLength(1); j++)
                w.Write(m[i, j]);
    }
    private static float[,] LoadMatrix(BinaryReader r)
    {
        int rows = r.ReadInt32(), cols = r.ReadInt32();
        var m = new float[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                m[i, j] = r.ReadSingle();
        return m;
    }
    private void SaveVector(BinaryWriter w, float[] v) { w.Write(v.Length); foreach (var x in v) w.Write(x); }
    private static float[] LoadVector(BinaryReader r) { int n = r.ReadInt32(); var v = new float[n]; for (int i = 0; i < n; i++) v[i] = r.ReadSingle(); return v; }

    public int Hidden1 => _hidden1;
    public int Hidden2 => _hidden2;
    public int OutputSize => _output;
    public int NumFeatures => _numFeatures;
    public int[] CategoryCounts => _categoryCounts;
    public int[] EmbeddingDims => _embeddingDims;
}

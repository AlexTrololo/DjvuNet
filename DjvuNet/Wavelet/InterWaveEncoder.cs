﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DjvuNet.Compression;

namespace DjvuNet.Wavelet
{
    public class InterWaveEncoder : InterWaveCodec
    {

        #region Constans

        /// <summary>
        /// Norm of all wavelets (for db estimation)
        /// </summary>
        public static readonly float[] iw_norm = new float [] 
        {
            2.627989e+03F,
            1.832893e+02F, 1.832959e+02F, 5.114690e+01F,
            4.583344e+01F, 4.583462e+01F, 1.279225e+01F,
            1.149671e+01F, 1.149712e+01F, 3.218888e+00F,
            2.999281e+00F, 2.999476e+00F, 8.733161e-01F,
            1.074451e+00F, 1.074511e+00F, 4.289318e-01F
        };

        /// <summary>
        /// Scale applied before decomposition
        /// </summary>
        public const int iw_shift = 6;

        #endregion Constans

        #region Fields

        internal InterWaveMap _EMap;

        #endregion Fields

        #region Constructors

        public InterWaveEncoder(InterWaveMap map) : base(map)
        {
            _EMap = new InterWaveMap(map.Height, map.Width);
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="zp"></param>
        /// <returns></returns>
        public int CodeSlice(IDataCoder zp)
        {
            // Check that code_slice can still run
            if (_CurrentBitPlane < 0)
                return 0;

            // Perform coding
            if (!IsNullSlice(_CurrentBitPlane, _CurrentBand))
            {
                for (int blockno = 0; blockno < _Map.BlockNumber; blockno++)
                {
                    int fbucket = _BandBuckets[_CurrentBand].Start;
                    int nbucket = _BandBuckets[_CurrentBand].Size;
                    EncodeBuckets(zp, _CurrentBitPlane, _CurrentBand,
                        _Map.Blocks[blockno], _EMap.Blocks[blockno],
                        fbucket, nbucket);
                }
            }
            return FinishCodeSlice(zp);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bit"></param>
        /// <param name="band"></param>
        /// <returns></returns>
        public bool IsNullSlice(int bit, int band)
        {
            if (band == 0)
            {
                bool is_null = true;
                for (int i = 0; i < 16; i++)
                {
                    int threshold = _QuantLow[i];
                    _CoefficientState[i] = ZERO;

                    if (threshold > 0 && threshold < 0x8000)
                    {
                        _CoefficientState[i] = UNK;
                        is_null = false;
                    }
                }
                return is_null;
            }
            else
            {
                int threshold = _QuantHigh[band];
                return (!(threshold > 0 && threshold < 0x8000));
            }
        }

        /// <summary>
        /// Function computes the states prior to encoding the buckets
        /// </summary>
        /// <param name="band"></param>
        /// <param name="fbucket"></param>
        /// <param name="nbucket"></param>
        /// <param name="blk"></param>
        /// <param name="eblk"></param>
        /// <returns></returns>
        public unsafe int EncodePrepare(int band, int fbucket, int nbucket, InterWaveBlock blk, InterWaveBlock eblk)
        {
            int bbstate = 0;
            // compute state of all coefficients in all buckets
            if (band != 0)
            {
                // Band other than zero
                int thres = _QuantHigh[band];
                GCHandle hCoeffState = GCHandle.Alloc(_CoefficientState, GCHandleType.Pinned);
                sbyte* cstate = (sbyte*) hCoeffState.AddrOfPinnedObject();

                for (int buckno = 0; buckno < nbucket; buckno++, cstate += 16)
                {
                    short[] pcoeff = blk.GetBlock(fbucket + buckno);
                    short[] epcoeff = eblk.GetBlock(fbucket + buckno);
                    int bstatetmp = 0;
                    if (null != pcoeff)
                    {
                        bstatetmp = UNK;
                        // cstate[i] is not used and does not need initialization
                    }
                    else if (null != epcoeff)
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            int cstatetmp = UNK;
                            if ((int)(pcoeff[i]) >= thres || (int)(pcoeff[i]) <= -thres)
                                cstatetmp = NEW | UNK;

                            cstate[i] = (sbyte) cstatetmp;
                            bstatetmp |= cstatetmp;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            int cstatetmp = UNK;

                            if (epcoeff[i] != 0)
                            {
                                cstatetmp = ACTIVE;
                            }
                            else if ((int)(pcoeff[i]) >= thres || (int)(pcoeff[i]) <= -thres)
                            {
                                cstatetmp = NEW | UNK;
                            }
                            cstate[i] = (sbyte) cstatetmp;
                            bstatetmp |= cstatetmp;
                        }
                    }
                    _BucketState[buckno] = (sbyte) bstatetmp;
                    bbstate |= bstatetmp;
                }
            }
            else
            {
                // Band zero ( fbucket==0 implies band==zero and nbucket==1 )
                short[] pcoeff = blk.GetInitializedBlock(0);
                short[] epcoeff = eblk.GetInitializedBlock(0);

                sbyte[] cstate = _CoefficientState;

                for (int i = 0; i < 16; i++)
                {
                    int thres = _QuantLow[i];
                    int cstatetmp = cstate[i];
                    if (cstatetmp != ZERO)
                    {
                        cstatetmp = UNK;
                        if (epcoeff[i] != 0)
                        {
                            cstatetmp = ACTIVE;
                        }
                        else if ((int)(pcoeff[i]) >= thres || (int)(pcoeff[i]) <= -thres)
                        {
                            cstatetmp = NEW | UNK;
                        }
                    }
                    cstate[i] = (sbyte) cstatetmp;
                    bbstate |= cstatetmp;
                }
                _BucketState[0] = (sbyte) bbstate;
            }
            return bbstate;
        }

        /// <summary>
        /// Function codes a sequence of buckets in a given block
        /// </summary>
        /// <param name="zp"></param>
        /// <param name="bit"></param>
        /// <param name="band"></param>
        /// <param name="blk"></param>
        /// <param name="eblk"></param>
        /// <param name="fbucket"></param>
        /// <param name="nbucket"></param>
        public void EncodeBuckets(IDataCoder zp, int bit, int band,
            InterWaveBlock blk, InterWaveBlock eblk, int fbucket, int nbucket)
        {
            // compute state of all coefficients in all buckets
            int bbstate = EncodePrepare(band, fbucket, nbucket, blk, eblk);

            // code root bit
            if ((nbucket < 16) || (bbstate & ACTIVE) != 0)
            {
                bbstate |= NEW;
            }
            else if ((bbstate & UNK) != 0)
            {
                zp.Encoder((bbstate & NEW) != 0 ? 1 : 0, ref _CtxRoot);
            }

            // code bucket bits
            if ((bbstate & NEW) != 0)
                for (int buckno = 0; buckno < nbucket; buckno++)
                {
                    // Code bucket bit
                    if ((_BucketState[buckno] & UNK) != 0)
                    {
                        // Context
                        int ctx = 0;
                        if (band > 0)
                        {
                            int k = (fbucket + buckno) << 2;
                            short[] b = eblk.GetBlock(k >> 4);

                            if (b != null)
                            {
                                k = k & 0xf;
                                if (b[k] != 0)
                                    ctx += 1;
                                if (b[k + 1] != 0)
                                    ctx += 1;
                                if (b[k + 2] != 0)
                                    ctx += 1;
                                if (ctx < 3 && b[k + 3] != 0)
                                    ctx += 1;
                            }
                        }

                        if ((bbstate & ACTIVE) != 0)
                            ctx |= 4;

                        // Code
                        zp.Encoder((_BucketState[buckno] & NEW) != 0 ? 1 : 0, ref _CtxBucket[band][ctx]);
                    }
                }

            // code new active coefficient (with their sign)
            if ((bbstate & NEW) != 0)
            {
                int thres = _QuantHigh[band];
                sbyte[] cstate = _CoefficientState;

                for (int buckno = 0, cidx = 0; buckno < nbucket; buckno++, cidx += 16)
                {
                    if ((_BucketState[buckno] & NEW) != 0)
                    {
                        int i;
                        int gotcha = 0;
                        const int maxgotcha = 7;

                        for (i = 0; i < 16; i++)
                        {
                            if ((cstate[i + cidx] & UNK) != 0)
                                gotcha += 1;
                        }

                        short[] pcoeff = blk.GetBlock(fbucket + buckno);
                        short[] epcoeff = eblk.GetInitializedBlock(fbucket + buckno);

                        // iterate within bucket
                        for (i = 0; i < 16; i++)
                        {
                            if ((cstate[i] & UNK) != 0)
                            {
                                // Prepare context
                                int ctx = 0;

                                if (gotcha >= maxgotcha)
                                    ctx = maxgotcha;
                                else
                                    ctx = gotcha;

                                if ((_BucketState[buckno] & ACTIVE) != 0)
                                    ctx |= 8;

                                // Code
                                zp.Encoder((cstate[i] & NEW) != 0 ? 1 : 0, ref _CtxStart[ctx]);

                                if ((cstate[i] & NEW) != 0)
                                {
                                    // Code sign
                                    zp.IWEncoder((pcoeff[i] < 0) ? true : false);

                                    // Set encoder state
                                    if (band == 0)
                                        thres = _QuantLow[i];

                                    epcoeff[i] = (short)(thres + (thres >> 1));
                                }

                                if ((cstate[i] & NEW) != 0)
                                    gotcha = 0;
                                else if (gotcha > 0)
                                    gotcha -= 1;
                            }
                        }
                    }
                }
            }

            // code mantissa bits
            if ((bbstate & ACTIVE) != 0)
            {
                int thres = _QuantHigh[band];
                sbyte[] cstate = _CoefficientState;

                for (int buckno = 0, cidx = 0; buckno < nbucket; buckno++, cidx += 16)
                {
                    if ((_BucketState[buckno] & ACTIVE) != 0)
                    {
                        short[] pcoeff = blk.GetBlock(fbucket + buckno);
                        short[] epcoeff = eblk.GetInitializedBlock(fbucket + buckno);

                        for (int i = 0; i < 16; i++)
                        {
                            if ((cstate[i] & ACTIVE) != 0)
                            {
                                // get coefficient
                                int coeff = pcoeff[i];
                                int ecoeff = epcoeff[i];
                                if (coeff < 0)
                                    coeff = -coeff;

                                // get band zero thresholds
                                if (band == 0)
                                    thres = _QuantLow[i];

                                // compute mantissa bit
                                int pix = 0;
                                if (coeff >= ecoeff)
                                    pix = 1;

                                // encode second or lesser mantissa bit
                                if (ecoeff <= 3 * thres)
                                    zp.Encoder(pix, ref _CtxMant);
                                else
                                    zp.IWEncoder(!!(pix != 0));

                                // adjust epcoeff
                                epcoeff[i] = (short)(ecoeff - (pix != 0 ? 0 : thres) + (thres >> 1));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frac"></param>
        /// <returns></returns>
        public unsafe float EstimateDecibel(float frac)
        {
            int i, j;
            float* q;
            // Fill norm arrays
            float[] normLo = new float[16];
            float[] normHi = new float[10];

            GCHandle iw = GCHandle.Alloc(iw_norm, GCHandleType.Pinned);
            // -- lo coefficients
            q = (float*)iw.AddrOfPinnedObject();

            for (i = j = 0; i < 4; j++)
                normLo[i++] = *q++;

            for (j = 0; j < 4; j++)
                normLo[i++] = *q;

            q += 1;
            for (j = 0; j < 4; j++)
                normLo[i++] = *q;

            q += 1;
            for (j = 0; j < 4; j++)
                normLo[i++] = *q;

            q += 1;

            // -- hi coefficients
            normHi[0] = 0;
            for (j = 1; j < 10; j++)
                normHi[j] = *q++;

            float[] xMse = new float[_Map.BlockNumber];

            for (int blockno = 0; blockno < _Map.BlockNumber; blockno++)
            {
                float vMse = 0.0f;

                // Iterate over bands
                for (int bandno = 0; bandno < 10; bandno++)
                {
                    int fbucket = _BandBuckets[bandno].Start;
                    int nbucket = _BandBuckets[bandno].Size;
                    InterWaveBlock blk = _Map.Blocks[blockno];
                    InterWaveBlock eblk = _EMap.Blocks[blockno];

                    float norm = normHi[bandno];

                    for (int buckno = 0; buckno < nbucket; buckno++)
                    {
                        short[] pcoeff = blk.GetBlock(fbucket + buckno);
                        short[] epcoeff = eblk.GetBlock(fbucket + buckno);

                        if (pcoeff != null)
                        {
                            if (epcoeff != null)
                            {
                                for (i = 0; i < 16; i++)
                                {
                                    if (bandno == 0)
                                        norm = normLo[i];
                                    float delta = (float)(pcoeff[i] < 0 ? -pcoeff[i] : pcoeff[i]);
                                    delta = delta - epcoeff[i];
                                    vMse = vMse + norm * delta * delta;
                                }
                            }
                            else
                            {
                                for (i = 0; i < 16; i++)
                                {
                                    if (bandno == 0)
                                        norm = normLo[i];
                                    float delta = (float)(pcoeff[i]);
                                    vMse = vMse + norm * delta * delta;
                                }
                            }
                        }
                    }
                }
                xMse[blockno] = vMse / 1024;
            }

            // Compute partition point
            int n = 0;
            int m = _Map.BlockNumber - 1;
            int p = (int)Math.Floor(m * (1.0 - frac) + 0.5);
            p = (p > m ? m : (p < 0 ? 0 : p));
            float pivot = 0;

            // Partition array
            while (n < p)
            {
                int l = n;
                int h = m;
                if (xMse[l] > xMse[h])
                {
                    float tmp = xMse[l];
                    xMse[l] = xMse[h];
                    xMse[h] = tmp;
                }

                pivot = xMse[(l + h) / 2];

                if (pivot < xMse[l])
                {
                    float tmp = pivot;
                    pivot = xMse[l];
                    xMse[l] = tmp;
                }

                if (pivot > xMse[h])
                {
                    float tmp = pivot;
                    pivot = xMse[h];
                    xMse[h] = tmp;
                }

                while (l < h)
                {
                    if (xMse[l] > xMse[h])
                    {
                        float tmp = xMse[l];
                        xMse[l] = xMse[h];
                        xMse[h] = tmp;
                    }

                    while (xMse[l] < pivot || (xMse[l] == pivot && l < h)) l++;

                    while (xMse[h] > pivot) h--;
                }

                if (p >= l)
                    n = l;
                else
                    m = l - 1;
            }

            // Compute average mse
            float mse = 0;

            for (i = p; i < _Map.BlockNumber; i++)
                mse = mse + xMse[i];

            mse = mse / (_Map.BlockNumber - p);

            // Return
            float factor = 255 << iw_shift;
            float decibel = (float)(10.0 * Math.Log(factor * factor / mse) / 2.302585125);
            return decibel;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="zp"></param>
        /// <returns></returns>
        public int FinishCodeSlice(IDataCoder zp)
        {
            // Reduce quantization threshold
            _QuantHigh[_CurrentBand] = _QuantHigh[_CurrentBand] >> 1;
            if (_CurrentBand == 0)
                for (int i = 0; i < 16; i++)
                    _QuantLow[i] = _QuantLow[i] >> 1;

            // Proceed to the next slice
            if (++_CurrentBand >= _BandBuckets.Length)
            {
                _CurrentBand = 0;
                _CurrentBitPlane += 1;
                if (_QuantHigh[_BandBuckets.Length - 1] == 0)
                {
                    _CurrentBitPlane = -1;
                    return 0;
                }
            }
            return 1;
        }

        #endregion Methods
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BrutalStory
{
    public struct BrutalStandardFxData
    {
        private float _minQuantity;
        private float _maxQuantity;
        private int _color;
        private float _minVelocityScale;
        private float _maxVelocityScale;
        private float _lifeLength = 1f;
        private float _gravityEffect = 1f;
        private float _minSize = 1f;
        private float _maxSize = 1f;
        private EnumParticleModel _model = EnumParticleModel.Cube;

        public BrutalStandardFxData(float minQuantity, float maxQuantity, int color, float minVelocityScale, float maxVelocityScale, float lifeLength, float gravityEffect, float minSize, float maxSize, EnumParticleModel model)
        {
            this._minQuantity = minQuantity;
            this._maxQuantity = maxQuantity;
            this._color = color;
            this._minVelocityScale = minVelocityScale;
            this._maxVelocityScale = maxVelocityScale;
            this._lifeLength = lifeLength;
            this._gravityEffect = gravityEffect;
            this._minSize = minSize;
            this._maxSize = maxSize;
            this._model = model;
        }

        public float minQuantity
        {
            get { return _minQuantity; }
        }

        public float maxQuantity
        {
            get { return _maxQuantity; }
        }

        public int color
        {
            get { return _color; }
        }

        public float minVelocityScale
        {
            get { return _minVelocityScale; }
        }

        public float maxVelocityScale
        {
            get { return _maxVelocityScale; }
        }

        public float lifeLength
        {
            get { return _lifeLength; }
        }

        public float gravityEffect
        {
            get { return _gravityEffect; }
        }

        public float minSize
        {
            get { return _minSize; }
        }

        public float maxSize
        {
            get { return _maxSize; }
        }

        public EnumParticleModel model
        {
            get { return _model; }
        }
    }

    public struct BrutalBloodyWaterFxData
    {
        private float _minQuantity;
        private float _maxQuantity;
        private int _color;
        private Vec3d _addPos;
        private Vec3f _addVelocity;

        public BrutalBloodyWaterFxData( float minQuantity, float maxQuantity, int color, Vec3d addPos, Vec3f addVelocity )
        {
            _minQuantity = minQuantity;
            _maxQuantity = maxQuantity;
            _color = color;
            _addPos = addPos;
            _addVelocity = addVelocity;
        }

        public float minQuantity
        {
            get { return _minQuantity; }
        }

        public float maxQuantity
        {
            get { return _maxQuantity; }
        }

        public int color
        {
            get { return _color; }
        }

        public Vec3d addPos
        {
            get { return _addPos; }
        }

        public Vec3f addVelocity
        {
            get { return _addVelocity; }
        }
    }
}

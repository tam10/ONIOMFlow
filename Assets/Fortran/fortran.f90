module Fortran
    implicit none
    integer, parameter :: sp=kind(1.0)
    real(sp), parameter :: pi=4*atan(1.0)
    !
    !
    ! Definitions:
    ! 
    ! VERTEX
    !   A vertex is a point that sits on the unit sphere
    !   Composed of 3 reals - x, y, z - called verts
    ! 
    ! FACE
    !   A face is a triangle that connects 3 vertices
    !   Composed of 3 integers called tris
    !   The tris must be ordered anti-clockwise facing the sphere
    !
    ! VERT
    !   x, y or z vector components of a vertex
    ! 
    ! VERTS
    !   Array of reals with length 3 * number of vertices
    !   Size can be computed as 30 * 4 ** resolution + 6
    !
    ! TRI
    !   Integer component of a face
    !   Array indexes >IN C#< to the geometry's vertices
    !
    ! TRIS
    !   Array of integers with length 3 * number of faces
    !   Size can be computed as 60 * 4 ** resolution for a sphere
    !
    !
    ! This routine relies on all atoms having the same number of verts
    !
    
    contains
    subroutine GetAllSpheres(verts, norms, tris, resolution, numVerts, numTris, &
        atomPositions, atomRadii, numAtoms)
        integer*4, intent(in) :: resolution, numVerts, numTris, numAtoms
        real(sp), intent(inout), dimension(numVerts) :: verts, norms
        integer*4, intent(inout), dimension(numTris) :: tris
        real(sp), intent(in), dimension(numAtoms * 3) :: atomPositions
        real(sp), intent(in), dimension(numAtoms) :: atomRadii

        integer*4 :: atomNum, vertexIndex
        
        integer*4 :: vertsPerAtom, trisPerAtom, verticesPerAtom
        real(sp), dimension(30 * 4**resolution + 6) :: vertsRef, coordShift
        real(sp), dimension(3) :: centre
        integer*4, dimension(60 * 4**resolution) :: trisRef

        !write(*,*) "Get all spheres"
        !call flush()

        ! Get the reference sphere for this resolution
        verticesPerAtom = 10 * 4**resolution + 2
        vertsPerAtom = verticesPerAtom * 3
        trisPerAtom = 60 * 4**resolution
        call GetSphereGeometry(vertsRef, trisRef, resolution, &
            vertsPerAtom, trisPerAtom)

        do atomNum = 1, numAtoms
            
            ! All verts need to be shifted
            call GetVertex(atomPositions, atomNum, centre)
            do vertexIndex = 1, verticesPerAtom
                coordShift(vertexIndex * 3 - 2 : vertexIndex * 3) = &
                centre(1:3)
            end do
            
            norms(vertsPerAtom * (atomNum - 1) + 1 : vertsPerAtom * atomNum) = &
                vertsRef(1 : vertsPerAtom)

            verts(vertsPerAtom * (atomNum - 1) + 1 : vertsPerAtom * atomNum) = &
                vertsRef(1 : vertsPerAtom) * atomRadii(atomNum) + &
                coordShift(1 : vertsPerAtom)

            tris(trisPerAtom * (atomNum - 1) + 1 : trisPerAtom * atomNum) = &
                trisRef(1 : trisPerAtom) + verticesPerAtom * (atomNum - 1)

        end do

    end subroutine GetAllSpheres

    subroutine GetSphereGeometry(verts, tris, resolution, numVerts, numTris)
        integer*4, intent(in) :: resolution, numVerts, numTris
        real(sp), intent(inout), dimension(numVerts) :: verts
        integer*4, intent(inout), dimension(numTris) :: tris

        integer*4 :: subdivision
        integer*8, dimension(numVerts) :: cachedVertKeys
        integer*4, dimension(numVerts) :: cachedVertIndices
        integer*4 :: numCached

        ! Get the basic icosahedron which will be refined into an icosphere
        call GetIcosahedronVertices(verts, numVerts)
        call GetIcosahedronTris(tris, numTris)

        numCached = 0
        cachedVertKeys = 0
        cachedVertIndices = 0

        if (resolution.gt.0) then
            do subdivision = 1, resolution
                call RefineAllFaces(verts, tris, numVerts, numTris, &
                    cachedVertKeys, cachedVertIndices, numCached, subdivision)
            end do
        end if

    end subroutine GetSphereGeometry

    subroutine GetAllCylinders(verts, norms, tris, resolution, numVerts, numTris, &
        atomPositions, atomRadii, numAtoms, bonds, numBonds, radiusRatio)
        ! Use a list of connections (bonds):
        !   (/ atom1atom1 atom1atom2 bond2atom1 bond2atom2 etc /))
        ! along with the coordinates to generate the tris and verts for a mesh
        ! 
        ! bonds have C# indexing
        integer*4, intent(in) :: resolution, numVerts, numTris, numAtoms, &
            numBonds
        real(sp), intent(in) :: radiusRatio
        real(sp), intent(inout), dimension(numVerts) :: verts, norms
        integer*4, intent(inout), dimension(numTris) :: tris
        integer*4, intent(in), dimension(numBonds * 2) :: bonds
        real(sp), intent(in), dimension(numAtoms * 3) :: atomPositions
        real(sp), intent(in), dimension(numAtoms) :: atomRadii
        
        integer*4 :: vertsPerBond, trisPerBond, verticesPerBond, &
            bondNum, bondIndex, atomIndex1, atomIndex2
        real(sp), dimension(6 * 2 ** (resolution + 2)) :: &
            vertsRef, normsRef
        real(sp), dimension(3) :: centre1, centre2, vector, bondCentre
        real(sp), dimension(3,3) :: R
        real(sp) :: bondLength, startLength, endLength, radius
        integer*4, dimension(6 * 2 ** (resolution + 2)) :: trisRef

        ! Get the reference cylinder
        verticesPerBond = 2 ** (resolution + 3)
        vertsPerBond = 3 * verticesPerBond
        trisPerBond = 3 * verticesPerBond

        bondIndex = 1
        do bondNum = 1, numBonds

            atomIndex1 = bonds(bondIndex)
            atomIndex2 = bonds(bondIndex + 1)

            ! Get the centres of the two bonds
            call GetVertex(atomPositions, atomIndex1, centre1)
            call GetVertex(atomPositions, atomIndex2, centre2)
            vector = centre2 - centre1

            radius = (atomRadii(atomIndex1) + atomRadii(atomIndex2)) * &
                0.5 * radiusRatio
            bondLength = length(vector)
            startLength = bondLength * 0.5 - atomRadii(atomIndex1)
            endLength = bondLength * 0.5 - atomRadii(atomIndex2)
            bondCentre = (centre2 + centre1) * 0.5

            call GetRotationMatrix( &
                vector / bondLength, (/ 0.0, 0.0, 1.0/), R)

            call GetCylinderGeometry(vertsRef, normsRef, trisRef, resolution, &
                vertsPerBond, trisPerBond, startLength, endLength, radius, &
                bondCentre, R)
            
            norms(vertsPerBond * (bondNum - 1) + 1 : vertsPerBond * bondNum) = &
                normsRef(1 : vertsPerBond)

            verts(vertsPerBond * (bondNum - 1) + 1 : vertsPerBond * bondNum) = &
                vertsRef(1 : vertsPerBond)

            tris(trisPerBond * (bondNum - 1) + 1 : trisPerBond * bondNum) = &
                trisRef(1 : trisPerBond) + verticesPerBond * (bondNum - 1)
    
            bondIndex = bondIndex + 2
        end do


    end subroutine

    subroutine GetCylinderGeometry(verts, norms, tris, resolution, &
            numVerts, numTris, startLength, endLength, radius, centre, rotationMatrix)
        integer*4, intent(in) :: resolution, numVerts, numTris
        real(sp), intent(inout), dimension(numVerts) :: verts, norms
        integer*4, intent(inout), dimension(numTris) :: tris
        real(sp), intent(in) :: startLength, endLength, radius
        real(sp), intent(in), dimension(3) :: centre
        real(sp), intent(in), dimension(3,3) :: rotationMatrix

        ! Get the tris and verts for a cylinder

        integer*4 :: subdivision, divisions, startIndex
        integer*4 :: v1, v2, v3, v4, vIndex
        real(sp) :: angle, x, y


        divisions = 2 ** (resolution + 2)

        vIndex = 0
        startIndex = 1
        do subdivision = 1, divisions
            angle = 2 * pi * subdivision / divisions 

            x = cos(angle) * radius
            y = sin(angle) * radius

            v1 = vIndex
            v2 = vIndex + 1

            if (subdivision == divisions) then
                v3 = 0
                v4 = 1
            else
                v3 = vIndex + 2
                v4 = vIndex + 3
            end if

            verts(startIndex : startIndex + 5) = &
            (/ x, y, -startLength, x, y, endLength /)

            norms(startIndex : startIndex + 5) = &
            (/ x, y, 0.0, x, y, 0.0 /)

            tris(startIndex : startIndex + 5) = &
            (/ v1, v3, v2, v2, v3, v4 /)

            !write(*,*) tris(startIndex : startIndex + 5)

            startIndex = startIndex + 6
            vIndex = vIndex + 2

        end do

        !write(*,*) tris
        !call flush()

        call RotateArrayWithMatrix(verts, rotationMatrix, 2 * divisions)
        call RotateArrayWithMatrix(norms, rotationMatrix, 2 * divisions)
        call Translate(verts, centre, 2 * divisions)

    end subroutine GetCylinderGeometry

    subroutine RefineAllFaces(verts, tris, numVerts, numTris, &
        cachedVertKeys, cachedVertIndices, numCached, subdivision)
        integer*4, intent(in) :: subdivision, numVerts, numTris
        real(sp), intent(inout), dimension(numVerts) :: verts
        integer*4, intent(inout), dimension(numTris) :: tris
        integer*8, intent(inout), dimension(numVerts) :: cachedVertKeys
        integer*4, intent(inout), dimension(numVerts) :: cachedVertIndices
        integer*4, intent(inout) :: numCached
        ! Must have the entire array for verts and tris
        ! numVerts must be 60 * 4 ** resolution
        ! numTris must be 10 * 4 ** resolution + 2

        integer*4 :: totalNumFaces, totalFaceNum, faceNum
        integer*4 :: totalVertexNum, numResVerts
        real(sp) :: normalisationFactor

        numResVerts = 30 * 4 ** subdivision + 6
        
        ! This gets updated in the loop
        totalVertexNum = (10 * 4 ** (subdivision - 1) + 2)
        totalFaceNum = 20 * 4 ** (subdivision - 1)
        totalNumFaces = totalFaceNum

        ! Get the distance to the first midPoint and use that as
        !   the normalisationFactor
        normalisationFactor = GetNormalisationFactor(verts, tris)

        ! Need to compute normalisationFactor
        ! Should be 0.5 * length of new vector
        do faceNum = 1, totalNumFaces
            call RefineFace(verts, tris, numVerts, numTris, faceNum, &
                totalVertexNum, totalFaceNum, &
                cachedVertKeys, cachedVertIndices, numCached, &
                normalisationFactor &
            )
        end do
        

    end subroutine RefineAllFaces

    subroutine RefineFace(verts, tris, numVerts, numTris, faceNum, &
        totalVertexNum, totalFaceNum, &
        cachedVertKeys, cachedVertIndices, numCached, &
        normalisationFactor)
        ! Split a triangle (face) into 4 triangles
        ! 
        ! Check to see if the verts already exist
        ! If they do, the triangles should refer to those
        ! Otherwise add new verts
        !
        ! Add 3 new outer triangles to the list, and change references
        ! of original triangle to be the new inner triangle
        !
        ! Only search the necessary number of verts for this particular
        ! resolution
        !
        ! normalisationFactor is used to bring new midPoints to the unit sphere
        ! All edges are the same length so this only needs to be computed once
        !  per resolution
        !
        integer*4, intent(in) :: faceNum, numVerts, numTris
        integer*4, intent(inout) :: totalVertexNum, totalFaceNum
        real(sp), intent(inout), dimension(numVerts) :: verts
        integer*4, intent(inout), dimension(numTris) :: tris

        integer*8, intent(inout), dimension(numVerts) :: cachedVertKeys
        integer*4, intent(inout), dimension(numVerts) :: cachedVertIndices
        integer*4, intent(inout) :: numCached
        real(sp), intent(in) :: normalisationFactor

        integer*4 :: faceIndex3, totalFaceIndex3, innerVertIndex
        integer*4 :: oIndex1, oIndex2, keyIndex
        integer*4, dimension(3) :: outerTriangle, innerTriangle
        integer*4, dimension(2) :: outerEdge
        integer*8 :: key
        logical :: cached

        !write(*,*) verts
        !call flush()

        ! Remember all tris are using C# indexing
        ! Only a problem when looking at verts
        faceIndex3 = faceNum * 3 - 3
        totalFaceIndex3 = totalFaceNum * 3

        call GetTriangle(tris, faceNum, outerTriangle)

        do oIndex1 = 1, 3

            oIndex2 = WrappedInteger(oIndex1 + 1, 1, 3)

            ! Sort edges so that they can be compared
            ! Also needed so they face the right way
            call GetOrderedEdge(outerEdge, outerTriangle(oIndex1), outerTriangle(oIndex2))

            ! Key to compare edge to see if vertex is already defined
            ! No need to search entire array - only for the number of
            !   verts for this resolution
            key = EdgeKey(outerEdge(1), outerEdge(2))

            cached = .false.
            do keyIndex = 1, numCached
                if (key.eq.cachedVertKeys(keyIndex)) then
                    innerVertIndex = cachedVertIndices(keyIndex)
                    cached = .true.
                    exit
                end if
            end do

            if (.not.cached) then
                ! Midpoint not cached. Create a new midpoint and normalise 
                ! to unit sphere

                ! Add new vert
                ! Outer edge contains C# indexes
                call SetMidVertex(verts, outerEdge, totalVertexNum, &
                    normalisationFactor)
                ! Refer to the latest added vert
                innerVertIndex = totalVertexNum
                totalVertexNum = totalVertexNum + 1
            end if
            
            ! Now innerVertIndex is either the cached index or the latest
            !   vertex index
            innerTriangle(oIndex1) = innerVertIndex

            numCached = numCached + 1
            cachedVertKeys(numCached) = key
            cachedVertIndices(numCached) = innerVertIndex
        end do

        ! Add new tris and swap indices of old tris
        tris(totalFaceIndex3+1:totalFaceIndex3+9) = (/ &
            innerTriangle(1), outerTriangle(2), innerTriangle(2), &
            innerTriangle(2), outerTriangle(3), innerTriangle(3), &
            innerTriangle(3), outerTriangle(1), innerTriangle(1) &
        /)
        call SetTriangle(tris, faceNum, innerTriangle)

        totalFaceNum = totalFaceNum + 3

    end subroutine RefineFace

    subroutine GetIcosahedronVertices(verts, numVerts)
        integer*4, intent(in) :: numVerts
        real(sp), intent(out), dimension(numVerts) :: verts
        real(sp) :: b, n
        integer*4 :: i

        integer*4, dimension(6) :: minusOne, plusOne, minusB, plusB

        n = (1.0 / sqrt(1.0 + ((1.0 + sqrt(5.0)) / 2.0) ** 2))
        b = ((1.0 + sqrt(5.0)) / 2.0) * n

        verts = 0

        minusOne = (/ 1, 7,  14, 20, 27, 33 /)
        plusOne =  (/ 4, 10, 17, 23, 30, 36 /)
        minusB =   (/ 8, 11, 21, 24, 31, 34 /)
        plusB =    (/ 2, 5,  15, 18, 25, 28 /)

        do i = 1, 6
            verts(minusOne(i)) = -n
            verts(plusOne(i)) = n
            verts(minusB(i)) = -b
            verts(plusB(i)) = b
        end do


    end subroutine GetIcosahedronVertices

    subroutine GetIcosahedronTris(tris, numTris)
        integer*4, intent(in) :: numTris
        integer*4, intent(out), dimension(numTris) :: tris

        !Need to use C# indexing
        tris = 0
        tris(1:60) = (/ &
             0,  1,  7, 7,  1, 8, 0,  7, 10, 10, 7,  6, 0, 10, 11, &
            11, 10,  2, 0, 11, 5, 5, 11,  4,  0, 5,  1, 1,  5,  9, &
             3,  6,  8, 8,  6, 7, 3,  2,  6,  6, 2, 10, 3,  4,  2, &
             2,  4, 11, 3,  9, 4, 4,  9,  5,  3, 8,  9, 9,  8,  1  &
        /)

    end subroutine GetIcosahedronTris

    subroutine GetTriangle(tris, triangleNum, outputTriangle)
        integer*4, intent(in), dimension(:) :: tris
        integer*4, intent(in) :: triangleNum
        integer*4, intent(inout), dimension(3) :: outputTriangle

        outputTriangle(1 : 3) = tris(triangleNum * 3 - 2 : triangleNum * 3)

    end subroutine GetTriangle

    subroutine GetVertex(verts, vertexNum, outputVertex)
        real(sp), intent(in), dimension(:) :: verts
        integer*4, intent(in) :: vertexNum
        real(sp), intent(inout), dimension(3) :: outputVertex

        outputVertex = verts(vertexNum * 3 - 2 : vertexNum * 3)

    end subroutine GetVertex

    subroutine GetVector(verts, vertexNum1, vertexNum2, outputVector)
        real(sp), intent(in), dimension(:) :: verts
        integer*4, intent(in) :: vertexNum1, vertexNum2
        real(sp), intent(inout), dimension(3) :: outputVector

        outputVector = verts(vertexNum2 * 3 - 2 : vertexNum2 * 3) - &
            verts(vertexNum1 * 3 - 2 : vertexNum1 * 3)

    end subroutine GetVector

    subroutine SetTriangle(tris, triangleNum, inputTriangle)
        integer*4, intent(inout), dimension(:) :: tris
        integer*4, intent(in) :: triangleNum
        integer*4, intent(in), dimension(3) :: inputTriangle

        tris(triangleNum * 3 - 2 : triangleNum * 3) = inputTriangle(1 : 3)

    end subroutine SetTriangle

    subroutine SetVertex(verts, vertexNum, inputVertex)
        real(sp), intent(inout), dimension(:) :: verts
        integer*4, intent(in) :: vertexNum
        real(sp), intent(in), dimension(3) :: inputVertex

        verts(vertexNum * 3 - 2 : vertexNum * 3) = inputVertex(1 : 3)

    end subroutine SetVertex

    function WrappedInteger(unwrappedValue, lowerBound, upperBound)
        integer*4 :: WrappedInteger
        integer*4, intent(in) :: unwrappedValue, lowerBound, upperBound

        if (unwrappedValue.gt.upperBound) then
            WrappedInteger = lowerBound
        else if (unwrappedValue.lt.lowerBound) then
            WrappedInteger = upperBound
        else
            WrappedInteger = unwrappedValue
        end if

    end function WrappedInteger

    subroutine GetOrderedEdge(edge, tri1, tri2)
        integer*4, intent(inout), dimension(2) :: edge
        integer*4, intent(in) :: tri1, tri2

        if (tri1.gt.tri2) then
            edge(1) = tri2
            edge(2) = tri1
        else 
            edge(1) = tri1
            edge(2) = tri2
        end if

    end subroutine GetOrderedEdge

    function EdgeKey(tri1, tri2)
        integer*8 :: EdgeKey
        integer*4, intent(in) :: tri1, tri2

        !EdgeKey = shiftl(tri1, 32) + tri2
        EdgeKey = tri1 * 2_8 ** 32 + tri2

    end function EdgeKey

    subroutine SetMidVertex(verts, edge, &
            outputVertexNum, normalisationFactor)
        real(sp), intent(inout), dimension(:) :: verts
        integer*4, intent(in), dimension(2) :: edge
        integer*4, intent(in) :: outputVertexNum
        real(sp), intent(in) :: normalisationFactor

        real(sp), dimension(3) :: vertex1, vertex2

        call GetVertex(verts, edge(1) + 1, vertex1)
        call GetVertex(verts, edge(2) + 1, vertex2)

        call SetVertex(verts, outputVertexNum + 1, &
            (vertex1 + vertex2) * normalisationFactor)

    end subroutine SetMidVertex

    function GetNormalisationFactor(verts, tris)
        real(sp) :: GetNormalisationFactor
        real(sp), intent(inout), dimension(:) :: verts
        integer*4, intent(inout), dimension(:) :: tris

        integer*4 :: j

        GetNormalisationFactor = 0
        do j = 1, 3
            GetNormalisationFactor = GetNormalisationFactor + &
                (verts(tris(1) * 3 + j) + verts(tris(2) * 3 + j)) ** 2
        end do
        GetNormalisationFactor = 1.0 / sqrt(GetNormalisationFactor)

    end function GetNormalisationFactor

    subroutine RotateArrayWithMatrix(A, R, numRows)
        real(sp), intent(inout), dimension(3 * numRows) :: A
        real(sp), intent(in), dimension(3,3) :: R
        integer*4, intent(in) :: numRows

        integer*4 :: row, rowIndex

        rowIndex = 1
        do row = 1, numRows
            A(rowIndex : rowIndex+2) = matmul(R, A(rowIndex : rowIndex+2))
            rowIndex = rowIndex + 3
        end do

    end subroutine RotateArrayWithMatrix

    subroutine RotateVectorWithMatrix(v, R)
        real(sp), intent(inout), dimension(3) :: v
        real(sp), intent(in), dimension(3,3) :: R
        v(1:3) = matmul(R, v(1:3))

    end subroutine RotateVectorWithMatrix

    subroutine RotateSubsetWithMatrix(A, R, start, end)
        real(sp), intent(inout), dimension(:) :: A
        real(sp), intent(in), dimension(3,3) :: R
        integer*4, intent(in) :: start, end

        integer*4 :: row, rowIndex

        rowIndex = (start * 3) + 1
        do row = start, end
            A(rowIndex : rowIndex+2) = matmul(R, A(rowIndex : rowIndex+2))
            rowIndex = rowIndex + 3
        end do

    end subroutine RotateSubsetWithMatrix

    subroutine Translate(A, centre, numRows)
        real(sp), intent(inout), dimension(:) :: A
        real(sp), intent(in), dimension(3) :: centre
        integer*4, intent(in) :: numRows

        integer*4 :: row, rowIndex

        rowIndex = 1
        do row = 1, numRows
            A(rowIndex : rowIndex+2) = A(rowIndex : rowIndex+2) + centre
            rowIndex = rowIndex + 3
        end do

    end subroutine Translate

    subroutine TranslateSubset(A, centre, start, end)
        real(sp), intent(inout), dimension(:) :: A
        real(sp), intent(in), dimension(3) :: centre
        integer*4, intent(in) :: start, end

        integer*4 :: row, rowIndex

        rowIndex = (start * 3) + 1
        do row = start, end
            A(rowIndex : rowIndex+2) = A(rowIndex : rowIndex+2) + centre
            rowIndex = rowIndex + 3
        end do

    end subroutine TranslateSubset

    subroutine GetRotationMatrix(v1n, v2n, R)
        ! Calculate the rotation matrix R that rotates
        !   unit vector v1n to unit vector v2n
        !
        real(sp), intent(in), dimension(3) :: v1n, v2n
        real(sp), intent(out), dimension(3,3) :: R

        real(sp), dimension(3) :: v
        real(sp), dimension(3,3) :: skew
        real(sp) :: s, c

        v = cross(v1n, v2n)
        s = length(v)
        c = dot_product(v1n, v2n)

        skew = reshape((/ &
            0.0, -v(3), v(2), &
            v(3), 0.0, -v(1), &
            -v(2), v(1), 0.0 &
        /), shape(skew))

        R = identity(3) + skew + ((1 - c) / (s ** 2)) * matmul(skew, skew) 

    end subroutine GetRotationMatrix

    function identity(size)
        real(sp), dimension(size, size) :: identity
        integer*4, intent(in) :: size

        integer*4 :: i

        identity = 0.0
        do i = 1, size
            identity(i, i) = 1.0
        end do

    end function identity

    function cross(v1, v2)
        real(sp), dimension(3) :: cross
        real(sp), intent(in) :: v1(3), v2(3)
        
        cross(1) = v1(2) * v2(3) - v1(3) * v2(2)
        cross(2) = v1(3) * v2(1) - v1(1) * v2(3)
        cross(3) = v1(1) * v2(2) - v1(2) * v2(1)
    end function cross

    function length_squared(v)
        real(sp) :: length_squared
        real(sp), intent(in), dimension(3) :: v

        length_squared = &
            v(1) ** 2 + &
            v(2) ** 2 + &
            v(3) ** 2

    end function length_squared

    function length(v)
        real(sp) :: length
        real(sp), intent(in), dimension(3) :: v

        length = sqrt(length_squared(v))

    end function length
    
end module Fortran
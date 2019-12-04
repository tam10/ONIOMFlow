module ArraysMod
    implicit none

    contains

    function distance_cs(ps, index0, index1)
        ! Distance between index0 and index1 in positions
        ! index0 and index1 are C#-like indices

        real*4, intent(in) :: ps(:)
        integer*4, intent(in) :: index0, index1
        real*4 :: distance_cs

        distance_cs = sqrt(distance2_cs(ps, index0, index1))

    end function distance_cs

    function distance2_cs(ps, index0, index1)
        ! Distance squared between index0 and index1 in positions
        ! index0 and index1 are C#-like indices

        real*4, intent(in) :: ps(:)
        integer*4, intent(in) :: index0, index1
        real*4 :: distance2_cs

        integer*4 :: i0, i1, j

        distance2_cs = 0
        i0 = index0 * 3
        i1 = index1 * 3

        do j = 1, 3
            distance2_cs = distance2_cs + (ps(i1+j) - ps(i0+j)) ** 2
        end do

    end function distance2_cs

    function distance(ps, index1, index2)
        ! Distance between index1 and index2 in positions

        real*4, intent(in) :: ps(:)
        integer*4, intent(in) :: index1, index2
        real*4 :: distance

        distance = sqrt(distance2(ps, index1, index2))

    end function distance

    function distance2(ps, index1, index2)
        ! Distance squared between index1 and index2 in positions

        real*4, intent(in) :: ps(:)
        integer*4, intent(in) :: index1, index2
        real*4 :: distance2

        integer*4 :: i1, i2, j

        distance2 = 0
        i1 = index1 * 3 - 3
        i2 = index2 * 3 - 3

        do j = 1, 3
            distance2 = distance2 + (ps(i2+j) - ps(i1+j)) ** 2
        end do

    end function distance2

    subroutine get_distance_matrix(ps, size, distance_matrix)
        integer*4, intent(in) :: size
        real*4, intent(in) :: ps(size * 3)
        real*4, intent(out) :: distance_matrix(size,size)

        integer*4 :: i, j
        real*4 :: r

        do i = 1, size - 1
            do j = i + 1, size
                r = distance(ps, i, j)
                distance_matrix(i,j) = r
                distance_matrix(j,i) = r
            end do
        end do

    end subroutine get_distance_matrix

end module
